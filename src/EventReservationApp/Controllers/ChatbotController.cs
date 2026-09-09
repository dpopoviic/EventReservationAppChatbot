
using System.Security.Claims;
using EventReservationApp.Models.ViewModels;
using EventReservationApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventReservationApp.Controllers;

[Authorize]
public class ChatbotController : Controller
{
    private readonly IChatbotConversationService _chatbotService;

    public ChatbotController(IChatbotConversationService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    // GET: /Chatbot
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    // POST: /Chatbot/Send
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(
        [FromBody] ChatRequestViewModel request,
        CancellationToken cancellationToken)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new
            {
                error = "Message cannot be empty."
            });
        }

        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var response =
            await _chatbotService.GetReplyAsync(
                userId,
                request.Message,
                cancellationToken);

        return Json(response);
    }

    // POST: /Chatbot/NewConversation
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewConversation(
        CancellationToken cancellationToken)
    {
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        await _chatbotService.StartNewConversationAsync(
            userId,
            cancellationToken);

        return Ok();
    }
}
