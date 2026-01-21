using System.ComponentModel.DataAnnotations;

namespace Phlox.API.DTOs;

public class LoginRequest
{
    [Required]
    public required string EmailOrUsername { get; set; }

    [Required]
    public required string Password { get; set; }
}
