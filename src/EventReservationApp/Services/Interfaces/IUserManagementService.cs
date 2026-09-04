using EventReservationApp.Models.ViewModels;

namespace EventReservationApp.Services.Interfaces;

/// <summary>
/// Administrator-facing user management operations, built on top of
/// ASP.NET Core Identity's UserManager/RoleManager rather than reimplementing
/// authentication logic.
/// </summary>
public interface IUserManagementService
{
    Task<List<UserListItemViewModel>> GetAllAsync();

    Task<UserEditViewModel?> GetForEditAsync(string userId);

    Task<(bool Success, string? ErrorMessage)> UpdateAsync(UserEditViewModel model);

    Task<(bool Success, string? ErrorMessage)> DeleteAsync(string userId);
}
