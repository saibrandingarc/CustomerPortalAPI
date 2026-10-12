using CustomerPortalAPI.Models;
using CustomerPortalAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace CustomerPortalAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class GoogleSheetController : ControllerBase
{
    private readonly GoogleServices _googleService;

    public GoogleSheetController(GoogleServices googleService)
    {
        _googleService = googleService;
    }

    [HttpGet]
    public async Task<ActionResult<List<GoogleSheetRow>>> GetRows()
    {
        try
        {
            var rows = await _googleService.ReadConfiguredSheetAsync();
            return Ok(rows);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
