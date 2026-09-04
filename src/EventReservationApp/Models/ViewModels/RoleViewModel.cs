using System.ComponentModel.DataAnnotations;

namespace EventReservationApp.Models.ViewModels;

public class RoleListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int UserCount { get; set; }
}

public class RoleFormViewModel
{
    public string? Id { get; set; }

    [Required]
    [StringLength(256)]
    [Display(Name = "Role Name")]
    public string Name { get; set; } = string.Empty;
}
