using EventReservationApp.Models.ViewModels;
using EventReservationApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventReservationApp.Controllers;

/// <summary>
/// User management for administrators. Registration of new users happens
/// through the standard Identity UI (/Identity/Account/Register); this
/// controller covers listing, editing profile/role, and deleting users.
/// </summary>
[Authorize(Roles = "Administrator")]
public class UsersController : Controller
{
    private readonly IUserManagementService _userManagementService;

    public UsersController(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    // GET: /Users
    public async Task<IActionResult> Index()
    {
        var users = await _userManagementService.GetAllAsync();
        return View(users);
    }

    // GET: /Users/Details/{id}
    public async Task<IActionResult> Details(string id)
    {
        var users = await _userManagementService.GetAllAsync();
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        return View(user);
    }

    // GET: /Users/Edit/{id}
    public async Task<IActionResult> Edit(string id)
    {
        var model = await _userManagementService.GetForEditAsync(id);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    // POST: /Users/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UserEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            var fresh = await _userManagementService.GetForEditAsync(id);
            model.AvailableRoles = fresh?.AvailableRoles ?? new List<string>();
            return View(model);
        }

        var (success, error) = await _userManagementService.UpdateAsync(model);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to update the user.");
            var fresh = await _userManagementService.GetForEditAsync(id);
            model.AvailableRoles = fresh?.AvailableRoles ?? new List<string>();
            return View(model);
        }

        TempData["StatusMessage"] = "User updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Users/Delete/{id}
    public async Task<IActionResult> Delete(string id)
    {
        var users = await _userManagementService.GetAllAsync();
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        return View(user);
    }

    // POST: /Users/Delete/{id}
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var (success, error) = await _userManagementService.DeleteAsync(id);
        TempData[success ? "StatusMessage" : "ErrorMessage"] = success ? "User deleted." : error;
        return RedirectToAction(nameof(Index));
    }
}
