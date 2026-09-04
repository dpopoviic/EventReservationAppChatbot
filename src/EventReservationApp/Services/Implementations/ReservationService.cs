using EventReservationApp.Data;
using EventReservationApp.Models.Entities;
using EventReservationApp.Models.ViewModels;
using EventReservationApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventReservationApp.Services.Implementations;

public class ReservationService : IReservationService
{
    private readonly ApplicationDbContext _context;

    public ReservationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ReservationListItemViewModel>> GetAllAsync()
    {
        return await Projected()
            .OrderByDescending(r => r.ReservationDate)
            .ToListAsync();
    }

    public async Task<List<ReservationListItemViewModel>> GetForUserAsync(string userId)
    {
        return await Projected()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ReservationDate)
            .ToListAsync();
    }

    public Task<ReservationListItemViewModel?> GetByIdAsync(int id) =>
        Projected().FirstOrDefaultAsync(r => r.Id == id);

    public async Task<(bool Success, string? ErrorMessage)> CreateAsync(string userId, int eventId, string? notes)
    {
        var @event = await _context.Events
            .Include(e => e.EventReservations)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (@event is null)
        {
            return (false, "The selected event could not be found.");
        }

        if (@event.EventReservations.Any(r => r.UserId == userId))
        {
            return (false, "You already have a reservation for this event.");
        }

        // Enforce capacity: reservations are blocked once an event is full.
        if (@event.EventReservations.Count >= @event.Capacity)
        {
            return (false, "This event has reached its capacity and can no longer accept reservations.");
        }

        var reservation = new EventReservation
        {
            UserId = userId,
            EventId = eventId,
            ReservationDate = DateTime.UtcNow,
            Notes = notes
        };

        _context.EventReservations.Add(reservation);
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var reservation = await _context.EventReservations.FirstOrDefaultAsync(r => r.Id == id);
        if (reservation is null)
        {
            return false;
        }

        _context.EventReservations.Remove(reservation);
        await _context.SaveChangesAsync();
        return true;
    }

    public Task<bool> IsOwnedByUserAsync(int reservationId, string userId) =>
        _context.EventReservations.AnyAsync(r => r.Id == reservationId && r.UserId == userId);

    private IQueryable<ReservationListItemViewModel> Projected() =>
        _context.EventReservations
            .Include(r => r.Event)
            .Include(r => r.User)
            .Select(r => new ReservationListItemViewModel
            {
                Id = r.Id,
                EventId = r.EventId,
                EventName = r.Event!.Name,
                EventStartDate = r.Event.StartDate,
                UserId = r.UserId,
                UserFullName = (r.User!.FirstName + " " + r.User.LastName).Trim(),
                UserEmail = r.User.Email ?? string.Empty,
                ReservationDate = r.ReservationDate,
                Notes = r.Notes
            });
}
