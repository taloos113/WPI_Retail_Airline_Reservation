using Microsoft.AspNetCore.Mvc;
using WpiReservation.Api.Models;
using WpiReservation.Api.Services;

namespace WpiReservation.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ReservationController(ReservationService reservationService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ReservationResponse>> Create([FromBody] ReservationRequest request)
    {
        try
        {
            return Ok(await reservationService.CreateAsync(request));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
