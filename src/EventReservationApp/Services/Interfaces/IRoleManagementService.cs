using EventReservationApp.Models.ViewModels;

namespace EventReservationApp.Services.Interfaces;

public interface IRoleManagementService
{
    Task<List<RoleListItemViewModel>> GetAllAsync();

    Task<RoleFormViewModel?> GetByIdAsync(string id);

    Task<(bool Success, string? ErrorMessage)> CreateAsync(RoleFormViewModel model);

    Task<(bool Success, string? ErrorMessage)> UpdateAsync(RoleFormViewModel model);

    Task<(bool Success, string? ErrorMessage)> DeleteAsync(string id);
}
