using EventReservationApp.Models.ViewModels;
using EventReservationApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventReservationApp.Controllers;

[Authorize(Roles = "Administrator")]
public class RolesController : Controller
{
    private readonly IRoleManagementService _roleManagementService;

    public RolesController(IRoleManagementService roleManagementService)
    {
        _roleManagementService = roleManagementService;
    }

    public async Task<IActionResult> Index()
    {
        var roles = await _roleManagementService.GetAllAsync();
        return View(roles);
    }

    public IActionResult Create() => View(new RoleFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RoleFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, error) = await _roleManagementService.CreateAsync(model);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to create role.");
            return View(model);
        }

        TempData["StatusMessage"] = "Role created successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var model = await _roleManagementService.GetByIdAsync(id);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, RoleFormViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, error) = await _roleManagementService.UpdateAsync(model);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to update role.");
            return View(model);
        }

        TempData["StatusMessage"] = "Role updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(string id)
    {
        var model = await _roleManagementService.GetByIdAsync(id);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var (success, error) = await _roleManagementService.DeleteAsync(id);
        TempData[success ? "StatusMessage" : "ErrorMessage"] = success ? "Role deleted." : error;
        return RedirectToAction(nameof(Index));
    }
}
