using MySqlConnector;
using WpiReservation.Api.Models;

namespace WpiReservation.Api.Services;

public class ReservationService(Db db, FlightService flightService)
{
    public async Task<ReservationResponse> CreateAsync(ReservationRequest request)
    {
        if (request.OutboundLegs.Count == 0) throw new ArgumentException("At least one outbound flight leg is required.");
        var allLegs = request.OutboundLegs.Concat(request.ReturnLegs ?? []).ToList();
        if (request.SelectedSeats.Count != allLegs.Count) throw new ArgumentException("Select exactly one seat for each flight leg.");

        var confirmationCode = GenerateConfirmationCode();
        var tripType = (request.ReturnLegs?.Count ?? 0) > 0 ? "round_trip" : "one_way";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            var reservationCmd = new MySqlCommand(@"INSERT INTO reservations(confirmation_code, trip_type, created_at)
                VALUES(@code, @tripType, UTC_TIMESTAMP()); SELECT LAST_INSERT_ID();", conn, (MySqlTransaction)tx);
            reservationCmd.Parameters.AddWithValue("@code", confirmationCode);
            reservationCmd.Parameters.AddWithValue("@tripType", tripType);
            var reservationId = Convert.ToInt64(await reservationCmd.ExecuteScalarAsync());

            for (var i = 0; i < allLegs.Count; i++)
            {
                var leg = allLegs[i];
                var seat = request.SelectedSeats.SingleOrDefault(s => s.FlightKey == leg.FlightKey)
                    ?? throw new ArgumentException($"Missing selected seat for flight {leg.FlightNumber}.");

                await flightService.EnsureSeatsAsync(conn, (MySqlTransaction)tx, leg.FlightKey);
                var seatCmd = new MySqlCommand(@"UPDATE seat_inventory
                    SET is_available=FALSE, reserved_confirmation_code=@code
                    WHERE flight_key=@flightKey AND seat_number=@seat AND is_available=TRUE;", conn, (MySqlTransaction)tx);
                seatCmd.Parameters.AddWithValue("@code", confirmationCode);
                seatCmd.Parameters.AddWithValue("@flightKey", leg.FlightKey);
                seatCmd.Parameters.AddWithValue("@seat", seat.SeatNumber);
                var updated = await seatCmd.ExecuteNonQueryAsync();
                if (updated != 1) throw new InvalidOperationException($"Seat {seat.SeatNumber} is no longer available for {leg.FlightNumber}.");

                var legCmd = new MySqlCommand(@"INSERT INTO reservation_flights(
                        reservation_id, sequence_number, direction, flight_key, airline, flight_number,
                        depart_airport, depart_code, arrive_airport, arrive_code, depart_local_datetime, arrive_local_datetime, seat_number)
                    VALUES(@reservationId, @seq, @direction, @flightKey, @airline, @flightNumber,
                        @departAirport, @departCode, @arriveAirport, @arriveCode, @depart, @arrive, @seat);", conn, (MySqlTransaction)tx);
                legCmd.Parameters.AddWithValue("@reservationId", reservationId);
                legCmd.Parameters.AddWithValue("@seq", i + 1);
                legCmd.Parameters.AddWithValue("@direction", request.OutboundLegs.Contains(leg) ? "outbound" : "return");
                legCmd.Parameters.AddWithValue("@flightKey", leg.FlightKey);
                legCmd.Parameters.AddWithValue("@airline", leg.Airline);
                legCmd.Parameters.AddWithValue("@flightNumber", leg.FlightNumber);
                legCmd.Parameters.AddWithValue("@departAirport", leg.DepartAirport);
                legCmd.Parameters.AddWithValue("@departCode", leg.DepartCode);
                legCmd.Parameters.AddWithValue("@arriveAirport", leg.ArriveAirport);
                legCmd.Parameters.AddWithValue("@arriveCode", leg.ArriveCode);
                legCmd.Parameters.AddWithValue("@depart", leg.DepartLocalDateTime);
                legCmd.Parameters.AddWithValue("@arrive", leg.ArriveLocalDateTime);
                legCmd.Parameters.AddWithValue("@seat", seat.SeatNumber);
                await legCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return new ReservationResponse(confirmationCode, tripType, request.SelectedSeats.Count, request.OutboundLegs, request.ReturnLegs ?? []);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static string GenerateConfirmationCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = Random.Shared;
        return new string(Enumerable.Range(0, 6).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}
