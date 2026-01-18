namespace Phlox.API.Entities;

public class UserEntity
{
    public Guid Id { get; set; }

    public required string KeycloakId { get; set; }

    public string? Email { get; set; }

    public string? Name { get; set; }

    public string? PreferredUsername { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public bool IsActive { get; set; } = true;
}
