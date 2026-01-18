using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phlox.API.DTOs;
using Phlox.API.Services;

namespace Phlox.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentUserService _currentUserService;

    public UserController(IUserService userService, ICurrentUserService currentUserService)
    {
        _userService = userService;
        _currentUserService = currentUserService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Unauthorized(new { message = "User is not authenticated" });
        }

        var user = await _userService.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            Name = user.Name,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            IsActive = user.IsActive
        });
    }
}
