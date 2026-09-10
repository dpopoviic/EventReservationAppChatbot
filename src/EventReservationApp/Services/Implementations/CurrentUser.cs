using EventReservationApp.Services.Interfaces;
using System.Security.Claims;

namespace EventReservationApp.Services.Implementations
{
    public class CurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUser(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

        public string? UserId => IsAuthenticated
            ? Principal!.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        public bool IsAdministrator => IsAuthenticated && Principal!.IsInRole("Administrator");

        public string RequireUserId() =>
            UserId ?? throw new UnauthorizedAccessException("This operation requires an authenticated user.");
    }

}
