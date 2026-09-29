namespace EventReservationApp.Services.Interfaces
{
    // Short-lived signed tokens that carry the signed-in user's id from this app,
    // through Rasa, back to the internal agent API. Tokens are signed with a
    // private key only this app holds; Rasa verifies them with the public key,
    // so it can check a token but never forge one for another user.
    public interface IUserTokenService
    {
        string CreateToken(string userId);

        // Returns the user id from a valid token, or null if the token is invalid,
        // expired, or not issued by this app.
        Task<string?> ValidateTokenAsync(string token);
    }
}
