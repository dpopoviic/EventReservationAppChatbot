using System.ComponentModel.DataAnnotations;

namespace EventReservationApp.Models.ViewModels;

/// <summary>
/// ViewModel used in the administrator's user list/details pages.
/// </summary>
public class UserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
    public bool EmailConfirmed { get; set; }
    public bool LockedOut { get; set; }
}

/// <summary>
/// ViewModel used by an administrator to edit a user's profile fields and
/// role assignment. Password changes are NOT handled here - Identity's own
/// mechanisms (reset password, change password) should be used for that.
/// </summary>
public class UserEditViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Locked Out")]
    public bool LockedOut { get; set; }

    [Required]
    [Display(Name = "Role")]
    public string SelectedRole { get; set; } = string.Empty;

    public IList<string> AvailableRoles { get; set; } = new List<string>();
}
