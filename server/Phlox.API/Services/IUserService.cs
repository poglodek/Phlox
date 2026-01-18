using Phlox.API.Entities;

namespace Phlox.API.Services;

public interface IUserService
{
    Task<UserEntity> GetOrCreateUserAsync(
        string keycloakId,
        string? email,
        string? name,
        string? preferredUsername,
        CancellationToken cancellationToken = default);

    Task<UserEntity?> GetUserByKeycloakIdAsync(
        string keycloakId,
        CancellationToken cancellationToken = default);

    Task UpdateLastLoginAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
