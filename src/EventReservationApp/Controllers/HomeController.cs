using System.Diagnostics;
using EventReservationApp.Models;
using EventReservationApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EventReservationApp.Controllers;

public class HomeController : Controller
{
    private readonly IEventService _eventService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IEventService eventService, ILogger<HomeController> logger)
    {
        _eventService = eventService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.Identity?.IsAuthenticated == true
            ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : null;

        var events = await _eventService.GetAllAsync(userId);
        return View(events.Take(6).ToList());
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
