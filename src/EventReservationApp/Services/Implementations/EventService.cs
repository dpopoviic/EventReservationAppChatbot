using EventReservationApp.Data;
using EventReservationApp.Models.Entities;
using EventReservationApp.Models.ViewModels;
using EventReservationApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventReservationApp.Services.Implementations;

public class EventService : IEventService
{
    private readonly ApplicationDbContext _context;

    public EventService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<EventListItemViewModel>> GetAllAsync(string? currentUserId = null)
    {
        var events = await _context.Events
            .OrderBy(e => e.StartDate)
            .Select(e => new EventListItemViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description,
                Location = e.Location,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                Capacity = e.Capacity,
                ReservedCount = e.EventReservations.Count,
                CurrentUserHasReservation = currentUserId != null &&
                    e.EventReservations.Any(r => r.UserId == currentUserId)
            })
            .ToListAsync();

        return events;
    }

    public Task<Event?> GetByIdAsync(int id) =>
        _context.Events.FirstOrDefaultAsync(e => e.Id == id);

    public async Task<EventListItemViewModel?> GetListItemByIdAsync(int id, string? currentUserId = null)
    {
        return await _context.Events
            .Where(e => e.Id == id)
            .Select(e => new EventListItemViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description,
                Location = e.Location,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                Capacity = e.Capacity,
                ReservedCount = e.EventReservations.Count,
                CurrentUserHasReservation = currentUserId != null &&
                    e.EventReservations.Any(r => r.UserId == currentUserId)
            })
            .FirstOrDefaultAsync();
    }

    public async Task<Event> CreateAsync(EventFormViewModel model)
    {
        var entity = new Event
        {
            Name = model.Name,
            Description = model.Description,
            Location = model.Location,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            Capacity = model.Capacity
        };

        _context.Events.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> UpdateAsync(EventFormViewModel model)
    {
        var entity = await _context.Events.FirstOrDefaultAsync(e => e.Id == model.Id);
        if (entity is null)
        {
            return false;
        }

        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.Location = model.Location;
        entity.StartDate = model.StartDate;
        entity.EndDate = model.EndDate;
        entity.Capacity = model.Capacity;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (entity is null)
        {
            return false;
        }

        _context.Events.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public Task<bool> ExistsAsync(int id) => _context.Events.AnyAsync(e => e.Id == id);
}
