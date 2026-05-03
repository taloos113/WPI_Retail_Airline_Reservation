using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MySqlConnector;
using WpiReservation.Api.Models;

namespace WpiReservation.Api.Services;

public class FlightService(Db db)
{
    private static readonly Regex CodeRegex = new(@"\(([A-Z]{3})\)$", RegexOptions.Compiled);

    public async Task<List<AirportDto>> GetAirportsAsync()
    {
        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        var airports = new Dictionary<string, AirportDto>();
        var cmd = new MySqlCommand(@"
            SELECT airport_name, airport_code, time_zone_id FROM airports
            UNION
            SELECT DISTINCT DepartAirport, SUBSTRING_INDEX(SUBSTRING_INDEX(DepartAirport, '(', -1), ')', 1), 'America/New_York' FROM deltas
            UNION
            SELECT DISTINCT ArriveAirport, SUBSTRING_INDEX(SUBSTRING_INDEX(ArriveAirport, '(', -1), ')', 1), 'America/New_York' FROM deltas
            UNION
            SELECT DISTINCT DepartAirport, SUBSTRING_INDEX(SUBSTRING_INDEX(DepartAirport, '(', -1), ')', 1), 'America/New_York' FROM southwests
            UNION
            SELECT DISTINCT ArriveAirport, SUBSTRING_INDEX(SUBSTRING_INDEX(ArriveAirport, '(', -1), ')', 1), 'America/New_York' FROM southwests
            ORDER BY airport_code;", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            var code = reader.GetString(1);
            var tz = reader.GetString(2);
            airports[code] = new AirportDto(code, StripCode(name), name, tz);
        }
        return airports.Values.OrderBy(a => a.Code).ToList();
    }

    public async Task<SearchResponse> SearchAsync(SearchRequest req)
    {
        var airports = await GetAirportsAsync();
        var airportByCode = airports.ToDictionary(a => a.Code, a => a);
        var fromCode = NormalizeAirportCode(req.DepartureAirport);
        var toCode = NormalizeAirportCode(req.ArrivalAirport);
        var rules = await GetConnectionRulesAsync();

        var outbound = await BuildItinerariesAsync(fromCode, toCode, req.DepartureDate, req.DepartureWindowStart, req.DepartureWindowEnd, rules, airportByCode);
        var returns = new List<ItineraryDto>();
        if (req.ReturnDate.HasValue)
        {
            returns = await BuildItinerariesAsync(toCode, fromCode, req.ReturnDate.Value, req.ReturnWindowStart, req.ReturnWindowEnd, rules, airportByCode);
        }

        return new SearchResponse(
            airports,
            SortItineraries(outbound, req.SortBy),
            SortItineraries(returns, req.SortBy)
        );
    }

    public async Task<SeatsResponse> GetSeatsAsync(string flightKey)
    {
        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await EnsureSeatsAsync(conn, null, flightKey);

        var cmd = new MySqlCommand("SELECT seat_number, is_available FROM seat_inventory WHERE flight_key=@flightKey ORDER BY row_number, seat_letter;", conn);
        cmd.Parameters.AddWithValue("@flightKey", flightKey);
        var seats = new List<SeatDto>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            seats.Add(new SeatDto(reader.GetString(0), reader.GetBoolean(1)));
        }
        return new SeatsResponse(flightKey, seats);
    }

    public async Task EnsureSeatsAsync(MySqlConnection conn, MySqlTransaction? tx, string flightKey)
    {
        var countCmd = new MySqlCommand("SELECT COUNT(*) FROM seat_inventory WHERE flight_key=@flightKey;", conn, tx);
        countCmd.Parameters.AddWithValue("@flightKey", flightKey);
        var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        if (count > 0) return;

        var letters = new[] { "A", "B", "C", "D" };
        for (var row = 1; row <= 8; row++)
        {
            foreach (var letter in letters)
            {
                var seat = $"{row}{letter}";
                var insert = new MySqlCommand(@"INSERT INTO seat_inventory(flight_key, seat_number, row_number, seat_letter, is_available)
                    VALUES(@flightKey, @seat, @row, @letter, TRUE);", conn, tx);
                insert.Parameters.AddWithValue("@flightKey", flightKey);
                insert.Parameters.AddWithValue("@seat", seat);
                insert.Parameters.AddWithValue("@row", row);
                insert.Parameters.AddWithValue("@letter", letter);
                await insert.ExecuteNonQueryAsync();
            }
        }
    }

    private async Task<(int Min, int Max)> GetConnectionRulesAsync()
    {
        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        var cmd = new MySqlCommand("SELECT min_layover_minutes, max_layover_minutes FROM connection_rules WHERE active=TRUE ORDER BY rule_id LIMIT 1;", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync()) return (reader.GetInt32(0), reader.GetInt32(1));
        return (45, 240);
    }

    private async Task<List<ItineraryDto>> BuildItinerariesAsync(string fromCode, string toCode, DateOnly date, string? start, string? end, (int Min, int Max) rules, Dictionary<string, AirportDto> airports)
    {
        var all = await GetFlightsForDateAsync(date);
        var direct = all.Where(f => f.DepartCode == fromCode && f.ArriveCode == toCode && InWindow(f.DepartLocalDateTime, start, end))
                        .Take(20)
                        .Select(f => ToItinerary([f]))
                        .ToList();

        var connections = new List<ItineraryDto>();
        var firstLegs = all.Where(f => f.DepartCode == fromCode && f.ArriveCode != toCode && InWindow(f.DepartLocalDateTime, start, end));
        foreach (var first in firstLegs)
        {
            var secondLegs = all.Where(f => f.DepartCode == first.ArriveCode && f.ArriveCode == toCode);
            foreach (var second in secondLegs)
            {
                var layover = MinutesBetween(first.ArriveLocalDateTime, second.DepartLocalDateTime);
                if (layover < rules.Min || layover > rules.Max) continue;
                var itinerary = ToItinerary([first, second]);
                if (itinerary.TotalTravelMinutes <= 720) connections.Add(itinerary);
            }
        }

        return direct.Concat(connections).DistinctBy(i => i.ItineraryId).Take(50).ToList();
    }

    private async Task<List<FlightLegDto>> GetFlightsForDateAsync(DateOnly date)
    {
        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        var cmd = new MySqlCommand(@"
            SELECT 'Delta' airline, FlightNumber, DepartAirport, ArriveAirport, DepartDateTime, ArriveDateTime FROM deltas WHERE DATE(DepartDateTime)=@date
            UNION ALL
            SELECT 'Southwest' airline, FlightNumber, DepartAirport, ArriveAirport, DepartDateTime, ArriveDateTime FROM southwests WHERE DATE(DepartDateTime)=@date
            ORDER BY DepartDateTime LIMIT 3000;", conn);
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
        var list = new List<FlightLegDto>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var airline = reader.GetString(0);
            var flightNumber = reader.GetString(1);
            var departAirport = reader.GetString(2);
            var arriveAirport = reader.GetString(3);
            var depart = reader.GetDateTime(4).ToString("yyyy-MM-dd HH:mm:ss");
            var arrive = reader.GetDateTime(5).ToString("yyyy-MM-dd HH:mm:ss");
            var departCode = ExtractCode(departAirport);
            var arriveCode = ExtractCode(arriveAirport);
            var key = StableKey(airline, flightNumber, departCode, arriveCode, depart);
            list.Add(new FlightLegDto(key, airline, flightNumber, departAirport, departCode, arriveAirport, arriveCode, depart, arrive, TimeZoneFor(departCode), TimeZoneFor(arriveCode)));
        }
        return list;
    }

    private static ItineraryDto ToItinerary(List<FlightLegDto> legs)
    {
        var total = MinutesBetween(legs.First().DepartLocalDateTime, legs.Last().ArriveLocalDateTime);
        var layover = 0;
        for (var i = 0; i < legs.Count - 1; i++) layover += MinutesBetween(legs[i].ArriveLocalDateTime, legs[i + 1].DepartLocalDateTime);
        var id = string.Join("_", legs.Select(l => l.FlightKey));
        return new ItineraryDto(id, legs, legs.Count - 1, Math.Max(total, 0), layover);
    }

    private static List<ItineraryDto> SortItineraries(List<ItineraryDto> source, string? sortBy)
    {
        return (sortBy?.ToLowerInvariant()) switch
        {
            "arrival" or "arrivaltime" => source.OrderBy(i => i.Legs.Last().ArriveLocalDateTime).ToList(),
            "traveltime" or "totaltraveltime" => source.OrderBy(i => i.TotalTravelMinutes).ThenBy(i => i.Legs.First().DepartLocalDateTime).ToList(),
            _ => source.OrderBy(i => i.Legs.First().DepartLocalDateTime).ToList()
        };
    }

    private static bool InWindow(string dateTime, string? start, string? end)
    {
        if (string.IsNullOrWhiteSpace(start) && string.IsNullOrWhiteSpace(end)) return true;
        var t = TimeOnly.FromDateTime(DateTime.Parse(dateTime));
        if (!string.IsNullOrWhiteSpace(start) && t < TimeOnly.Parse(start)) return false;
        if (!string.IsNullOrWhiteSpace(end) && t > TimeOnly.Parse(end)) return false;
        return true;
    }

    private static int MinutesBetween(string start, string end)
    {
        var diff = DateTime.Parse(end) - DateTime.Parse(start);
        // Sample data stores local wall-clock times. Some flights cross midnight/time-zone boundaries,
        // so a same-date arrival can appear earlier than departure. Treat that as next-day arrival.
        if (diff.TotalMinutes < 0) diff = diff.Add(TimeSpan.FromDays(1));
        return (int)Math.Round(diff.TotalMinutes);
    }

    public static string NormalizeAirportCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var upper = value.Trim().ToUpperInvariant();
        var match = Regex.Match(upper, @"([A-Z]{3})");
        return match.Success ? match.Groups[1].Value : upper;
    }

    private static string ExtractCode(string airport)
    {
        var match = CodeRegex.Match(airport);
        return match.Success ? match.Groups[1].Value : NormalizeAirportCode(airport);
    }

    private static string StripCode(string airport) => Regex.Replace(airport, @"\s*\([A-Z]{3}\)$", "");

    private static string StableKey(params string[] parts)
    {
        var input = string.Join("|", parts);
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }

    private static string TimeZoneFor(string code) => code switch
    {
        "LAX" or "SFO" or "SEA" or "SAN" or "SJC" or "OAK" or "BUR" or "SNA" or "ONT" or "SMF" or "LGB" => "America/Los_Angeles",
        "DEN" or "SLC" or "PHX" or "LAS" or "TUS" or "BOI" or "ABQ" => "America/Denver",
        "ORD" or "MDW" or "MSP" or "DAL" or "DFW" or "HOU" or "IAH" or "MCI" or "STL" or "BNA" or "AUS" or "SAT" or "MSY" => "America/Chicago",
        _ => "America/New_York"
    };
}
