using System.Security.Claims;
using EventReservationApp.Models.ViewModels;
using EventReservationApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventReservationApp.Controllers;

/// <summary>
/// Serves the chatbot placeholder page and its message endpoint.
/// All actual "thinking" happens inside IChatbotService - see
/// PlaceholderChatbotService for the current no-AI implementation and its
/// XML docs for how to swap in a real AI provider later.
/// </summary>
[Authorize]
public class ChatbotController : Controller
{
    private readonly IChatbotService _chatbotService;

    public ChatbotController(IChatbotService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    // GET: /Chatbot
    public IActionResult Index() => View();

    // POST: /Chatbot/Send
    // Called via fetch() from the chatbot page's JavaScript.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send([FromBody] ChatRequestViewModel request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message cannot be empty." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var response = await _chatbotService.GetReplyAsync(userId, request.Message);
        return Json(response);
    }
}
