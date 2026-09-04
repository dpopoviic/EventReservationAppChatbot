using EventReservationApp.Models.Entities;
using EventReservationApp.Models.ViewModels;
using EventReservationApp.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventReservationApp.Services.Implementations;

public class RoleManagementService : IRoleManagementService
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public RoleManagementService(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
    {
        _roleManager = roleManager;
        _userManager = userManager;
    }

    public async Task<List<RoleListItemViewModel>> GetAllAsync()
    {
        var roles = await _roleManager.Roles.ToListAsync();
        var result = new List<RoleListItemViewModel>();

        foreach (var role in roles)
        {
            // IdentityRole has no Users navigation property by default, so we ask
            // UserManager (which queries the AspNetUserRoles join table) instead.
            var usersInRole = role.Name is null
                ? 0
                : (await _userManager.GetUsersInRoleAsync(role.Name)).Count;

            result.Add(new RoleListItemViewModel
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                UserCount = usersInRole
            });
        }

        return result;
    }

    public async Task<RoleFormViewModel?> GetByIdAsync(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        return role is null ? null : new RoleFormViewModel { Id = role.Id, Name = role.Name ?? string.Empty };
    }

    public async Task<(bool Success, string? ErrorMessage)> CreateAsync(RoleFormViewModel model)
    {
        if (await _roleManager.RoleExistsAsync(model.Name))
        {
            return (false, "A role with this name already exists.");
        }

        var result = await _roleManager.CreateAsync(new IdentityRole(model.Name));
        return result.Succeeded
            ? (true, null)
            : (false, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(RoleFormViewModel model)
    {
        if (model.Id is null)
        {
            return (false, "Role id is required.");
        }

        var role = await _roleManager.FindByIdAsync(model.Id);
        if (role is null)
        {
            return (false, "Role not found.");
        }

        role.Name = model.Name;
        var result = await _roleManager.UpdateAsync(role);
        return result.Succeeded
            ? (true, null)
            : (false, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null)
        {
            return (false, "Role not found.");
        }

        if (role.Name is DbInitializerRoleNames.Administrator or DbInitializerRoleNames.Customer)
        {
            return (false, "Built-in roles (Administrator, Customer) cannot be deleted.");
        }

        var result = await _roleManager.DeleteAsync(role);
        return result.Succeeded
            ? (true, null)
            : (false, string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}

/// <summary>Small helper so the service doesn't need to reference Data.DbInitializer directly.</summary>
internal static class DbInitializerRoleNames
{
    public const string Administrator = "Administrator";
    public const string Customer = "Customer";
}
