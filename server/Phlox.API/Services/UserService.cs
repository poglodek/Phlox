using Microsoft.EntityFrameworkCore;
using Phlox.API.Data;
using Phlox.API.Entities;

namespace Phlox.API.Services;

public class UserService : IUserService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UserService> _logger;

    public UserService(ApplicationDbContext dbContext, ILogger<UserService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UserEntity> GetOrCreateUserAsync(
        string keycloakId,
        string? email,
        string? name,
        string? preferredUsername,
        CancellationToken cancellationToken = default)
    {
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId, cancellationToken);

        if (existingUser is not null)
        {
            // Update user info if changed
            var updated = false;

            if (existingUser.Email != email)
            {
                existingUser.Email = email;
                updated = true;
            }

            if (existingUser.Name != name)
            {
                existingUser.Name = name;
                updated = true;
            }

            if (existingUser.PreferredUsername != preferredUsername)
            {
                existingUser.PreferredUsername = preferredUsername;
                updated = true;
            }

            existingUser.LastLoginAt = DateTime.UtcNow;

            if (updated)
            {
                _logger.LogInformation(
                    "Updated user info for KeycloakId: {KeycloakId}",
                    keycloakId);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return existingUser;
        }

        // Create new user
        var newUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            KeycloakId = keycloakId,
            Email = email,
            Name = name,
            PreferredUsername = preferredUsername,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsActive = true
        };

        _dbContext.Users.Add(newUser);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created new user with KeycloakId: {KeycloakId}, Email: {Email}",
            keycloakId,
            email);

        return newUser;
    }

    public async Task<UserEntity?> GetUserByKeycloakIdAsync(
        string keycloakId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId, cancellationToken);
    }

    public async Task UpdateLastLoginAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.LastLoginAt, DateTime.UtcNow),
                cancellationToken);
    }
}
