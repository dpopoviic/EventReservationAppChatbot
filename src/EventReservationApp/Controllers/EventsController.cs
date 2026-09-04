using EventReservationApp.Models.ViewModels;
using EventReservationApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventReservationApp.Controllers;

/// <summary>
/// Anyone (including anonymous visitors) can browse events. Creating,
/// editing and deleting events is restricted to Administrators - enforced
/// here on the server side via [Authorize(Roles = "Administrator")], not
/// just hidden in the UI.
/// </summary>
public class EventsController : Controller
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    private string? CurrentUserId =>
        User.Identity?.IsAuthenticated == true
            ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : null;

    // GET: /Events
    public async Task<IActionResult> Index()
    {
        var events = await _eventService.GetAllAsync(CurrentUserId);
        return View(events);
    }

    // GET: /Events/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var item = await _eventService.GetListItemByIdAsync(id, CurrentUserId);
        if (item is null)
        {
            return NotFound();
        }

        return View(item);
    }

    // GET: /Events/Create
    [Authorize(Roles = "Administrator")]
    public IActionResult Create() => View(new EventFormViewModel());

    // POST: /Events/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Create(EventFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.EndDate <= model.StartDate)
        {
            ModelState.AddModelError(nameof(model.EndDate), "End date must be after the start date.");
            return View(model);
        }

        await _eventService.CreateAsync(model);
        TempData["StatusMessage"] = "Event created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Events/Edit/5
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _eventService.GetByIdAsync(id);
        if (entity is null)
        {
            return NotFound();
        }

        var model = new EventFormViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Location = entity.Location,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Capacity = entity.Capacity
        };

        return View(model);
    }

    // POST: /Events/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Edit(int id, EventFormViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.EndDate <= model.StartDate)
        {
            ModelState.AddModelError(nameof(model.EndDate), "End date must be after the start date.");
            return View(model);
        }

        var updated = await _eventService.UpdateAsync(model);
        if (!updated)
        {
            return NotFound();
        }

        TempData["StatusMessage"] = "Event updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Events/Delete/5
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _eventService.GetListItemByIdAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        return View(item);
    }

    // POST: /Events/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _eventService.DeleteAsync(id);
        TempData["StatusMessage"] = "Event deleted.";
        return RedirectToAction(nameof(Index));
    }
}
