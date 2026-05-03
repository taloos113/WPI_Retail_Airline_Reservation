namespace WpiReservation.Api.Models;

public record AirportDto(string Code, string Name, string DisplayName, string TimeZoneId);

public record FlightLegDto(
    string FlightKey,
    string Airline,
    string FlightNumber,
    string DepartAirport,
    string DepartCode,
    string ArriveAirport,
    string ArriveCode,
    string DepartLocalDateTime,
    string ArriveLocalDateTime,
    string DepartTimeZone,
    string ArriveTimeZone
);

public record ItineraryDto(
    string ItineraryId,
    List<FlightLegDto> Legs,
    int Stops,
    int TotalTravelMinutes,
    int TotalLayoverMinutes
);

public class SearchRequest
{
    public string DepartureAirport { get; set; } = string.Empty;
    public string ArrivalAirport { get; set; } = string.Empty;
    public DateOnly DepartureDate { get; set; }
    public string? DepartureWindowStart { get; set; }
    public string? DepartureWindowEnd { get; set; }
    public DateOnly? ReturnDate { get; set; }
    public string? ReturnWindowStart { get; set; }
    public string? ReturnWindowEnd { get; set; }
    public string? SortBy { get; set; }
}

public record SearchResponse(List<AirportDto> Airports, List<ItineraryDto> OutboundItineraries, List<ItineraryDto> ReturnItineraries);

public record SeatDto(string SeatNumber, bool IsAvailable);
public record SeatsResponse(string FlightKey, List<SeatDto> Seats);

public record SelectedSeatDto(string FlightKey, string SeatNumber);
public record ReservationRequest(List<FlightLegDto> OutboundLegs, List<FlightLegDto>? ReturnLegs, List<SelectedSeatDto> SelectedSeats);
public record ReservationResponse(string ConfirmationCode, string TripType, int ReservedSeats, List<FlightLegDto> OutboundLegs, List<FlightLegDto> ReturnLegs);
