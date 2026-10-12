using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CustomerPortalAPI.Controllers;
using CustomerPortalAPI.Services;
using CustomerPortalAPI.Models;

namespace CustomerPortalAPI.Controllers;

[ApiController]
[Route("/meta/[controller]")]
public class GenericController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public GenericController(ILogger<GenericController> logger,
           ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Route("GetStaff")]
    public async Task<List<Staff>> GetStaff()
    {
        return await _context.Staff.ToListAsync();
    }

    [HttpGet]
    [Route("GetMainMenu")]
    public async Task<List<MainMenu>> GetMainMenu()
    {
        return await _context.MainMenu.ToListAsync();
    }
}