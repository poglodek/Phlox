namespace Phlox.API.Services;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Username { get; }
    bool IsAuthenticated { get; }
}
