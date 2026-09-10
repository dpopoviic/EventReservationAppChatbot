namespace EventReservationApp.Services.Interfaces
{
    public interface ICurrentUser
    {
        string? UserId { get; }
        bool IsAuthenticated { get; }
        bool IsAdministrator { get; }
        string RequireUserId();
    }
}
