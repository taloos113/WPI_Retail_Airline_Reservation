using Microsoft.AspNetCore.Mvc;
using WpiReservation.Api.Models;
using WpiReservation.Api.Services;

namespace WpiReservation.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class FlightController(FlightService flightService) : ControllerBase
{
    [HttpGet("airports")]
    public async Task<ActionResult<List<AirportDto>>> GetAirports() => await flightService.GetAirportsAsync();

    [HttpGet("search")]
    public async Task<ActionResult<SearchResponse>> Search([FromQuery] SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DepartureAirport) || string.IsNullOrWhiteSpace(request.ArrivalAirport))
            return BadRequest("Departure and arrival airports are required.");
        return await flightService.SearchAsync(request);
    }

    [HttpGet("seats/{flightKey}")]
    public async Task<ActionResult<SeatsResponse>> GetSeats(string flightKey) => await flightService.GetSeatsAsync(flightKey);
}
