namespace Phlox.API.DTOs;

public class AuthResponse
{
    public required string Token { get; set; }
    public required Guid UserId { get; set; }
    public required string Email { get; set; }
    public required string Username { get; set; }
    public string? Name { get; set; }
    public DateTime ExpiresAt { get; set; }
}
