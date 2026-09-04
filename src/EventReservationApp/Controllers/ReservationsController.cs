using System.Security.Claims;
using EventReservationApp.Models.ViewModels;
using EventReservationApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventReservationApp.Controllers;

/// <summary>
/// Every action requires authentication. Administrators can see and manage
/// all reservations; customers can only see/manage their own - this is
/// enforced in each action, not just hidden in the UI.
/// </summary>
[Authorize]
public class ReservationsController : Controller
{
    private readonly IReservationService _reservationService;
    private readonly IEventService _eventService;

    public ReservationsController(IReservationService reservationService, IEventService eventService)
    {
        _reservationService = reservationService;
        _eventService = eventService;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private bool IsAdministrator => User.IsInRole("Administrator");

    // GET: /Reservations  (Administrator: all reservations)
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Index()
    {
        var reservations = await _reservationService.GetAllAsync();
        return View(reservations);
    }

    // GET: /Reservations/Mine  (Customer/any authenticated user: their own reservations)
    public async Task<IActionResult> Mine()
    {
        var reservations = await _reservationService.GetForUserAsync(CurrentUserId);
        return View(reservations);
    }

    // GET: /Reservations/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var reservation = await _reservationService.GetByIdAsync(id);
        if (reservation is null)
        {
            return NotFound();
        }

        if (!IsAdministrator && reservation.UserId != CurrentUserId)
        {
            return Forbid();
        }

        return View(reservation);
    }

    // GET: /Reservations/Create/5   (5 = eventId)
    public async Task<IActionResult> Create(int eventId)
    {
        var @event = await _eventService.GetListItemByIdAsync(eventId, CurrentUserId);
        if (@event is null)
        {
            return NotFound();
        }

        if (@event.IsFull)
        {
            TempData["ErrorMessage"] = "This event has reached its capacity.";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        if (@event.CurrentUserHasReservation)
        {
            TempData["ErrorMessage"] = "You already have a reservation for this event.";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        return View(new ReservationCreateViewModel { EventId = eventId, EventName = @event.Name });
    }

    // POST: /Reservations/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReservationCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, errorMessage) = await _reservationService.CreateAsync(CurrentUserId, model.EventId, model.Notes);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, errorMessage ?? "Unable to create the reservation.");
            return View(model);
        }

        TempData["StatusMessage"] = "Reservation created successfully.";
        return RedirectToAction(nameof(Mine));
    }

    // GET: /Reservations/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        var reservation = await _reservationService.GetByIdAsync(id);
        if (reservation is null)
        {
            return NotFound();
        }

        if (!IsAdministrator && reservation.UserId != CurrentUserId)
        {
            return Forbid();
        }

        return View(reservation);
    }

    // POST: /Reservations/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!IsAdministrator && !await _reservationService.IsOwnedByUserAsync(id, CurrentUserId))
        {
            return Forbid();
        }

        await _reservationService.DeleteAsync(id);
        TempData["StatusMessage"] = "Reservation cancelled.";
        return RedirectToAction(IsAdministrator ? nameof(Index) : nameof(Mine));
    }
}
