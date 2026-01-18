using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phlox.API.Services;

namespace Phlox.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var keycloakId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(keycloakId))
        {
            return Unauthorized("User identifier not found in token");
        }

        var email = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email");
        var name = User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("name");
        var preferredUsername = User.FindFirstValue("preferred_username");

        // Sync user to database (creates if not exists, updates if changed)
        var userEntity = await _userService.GetOrCreateUserAsync(
            keycloakId,
            email,
            name,
            preferredUsername,
            cancellationToken);

        var roles = User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        if (roles.Count == 0)
        {
            roles = User.FindAll("realm_access")
                .SelectMany(c => ParseRealmRoles(c.Value))
                .ToList();
        }

        return Ok(new
        {
            Id = userEntity.Id,
            KeycloakId = userEntity.KeycloakId,
            Email = userEntity.Email,
            Name = userEntity.Name ?? userEntity.PreferredUsername,
            PreferredUsername = userEntity.PreferredUsername,
            Roles = roles,
            CreatedAt = userEntity.CreatedAt,
            LastLoginAt = userEntity.LastLoginAt,
            IsActive = userEntity.IsActive
        });
    }

    private static IEnumerable<string> ParseRealmRoles(string realmAccessJson)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(realmAccessJson);
            if (doc.RootElement.TryGetProperty("roles", out var rolesElement))
            {
                return rolesElement.EnumerateArray()
                    .Select(r => r.GetString())
                    .Where(r => r != null)
                    .Cast<string>();
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        return [];
    }
}
