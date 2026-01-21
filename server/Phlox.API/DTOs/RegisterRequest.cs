using System.ComponentModel.DataAnnotations;

namespace Phlox.API.DTOs;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public required string Email { get; set; }

    [Required]
    [MaxLength(255)]
    [MinLength(3)]
    public required string Username { get; set; }

    [Required]
    [MinLength(8)]
    public required string Password { get; set; }

    [MaxLength(255)]
    public string? Name { get; set; }
}
