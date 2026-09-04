using EventReservationApp.Data;
using EventReservationApp.Models.Entities;
using EventReservationApp.Models.ViewModels;
using EventReservationApp.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventReservationApp.Services.Implementations;

/// <summary>
/// Wraps ASP.NET Core Identity's UserManager/RoleManager for the
/// administrator's user-management screens. Never touches password hashes
/// directly - all of that stays inside Identity.
/// </summary>
public class UserManagementService : IUserManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;

    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    public async Task<List<UserListItemViewModel>> GetAllAsync()
    {
        var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
        var result = new List<UserListItemViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserListItemViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Roles = roles,
                EmailConfirmed = user.EmailConfirmed,
                LockedOut = user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow
            });
        }

        return result;
    }

    public async Task<UserEditViewModel?> GetForEditAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        var allRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();

        return new UserEditViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            LockedOut = user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow,
            SelectedRole = roles.FirstOrDefault() ?? string.Empty,
            AvailableRoles = allRoles
        };
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(UserEditViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user is null)
        {
            return (false, "User not found.");
        }

        user.Email = model.Email;
        user.UserName = model.Email;
        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.LockoutEnd = model.LockedOut ? DateTimeOffset.MaxValue : null;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return (false, string.Join("; ", updateResult.Errors.Select(e => e.Description)));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(model.SelectedRole))
        {
            if (currentRoles.Count > 0)
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            if (!string.IsNullOrWhiteSpace(model.SelectedRole))
            {
                await _userManager.AddToRoleAsync(user, model.SelectedRole);
            }
        }

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return (false, "User not found.");
        }

        var hasReservations = await _context.EventReservations.AnyAsync(r => r.UserId == userId);
        if (hasReservations)
        {
            return (false, "This user has existing reservations and cannot be deleted. Remove their reservations first.");
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return (true, null);
    }
}
