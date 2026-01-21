using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Phlox.API.Configuration;
using Phlox.API.DTOs;
using Phlox.API.Services;

namespace Phlox.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserService userService,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthController> logger)
    {
        _userService = userService;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (await _userService.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            return Conflict(new { message = "Email is already registered" });
        }

        if (await _userService.ExistsByUsernameAsync(request.Username, cancellationToken))
        {
            return Conflict(new { message = "Username is already taken" });
        }

        var passwordHash = _passwordHasher.Hash(request.Password);

        var user = await _userService.CreateAsync(
            request.Email,
            request.Username,
            passwordHash,
            request.Name,
            cancellationToken);

        var token = _tokenService.GenerateToken(user);

        _logger.LogInformation("User registered: {Email}", user.Email);

        return Ok(new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            Username = user.Username,
            Name = user.Name,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes)
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByEmailOrUsernameAsync(request.EmailOrUsername, cancellationToken);

        if (user is null)
        {
            return Unauthorized(new { message = "Invalid credentials" });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new { message = "Account is deactivated" });
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid credentials" });
        }

        await _userService.UpdateLastLoginAsync(user.Id, cancellationToken);

        var token = _tokenService.GenerateToken(user);

        _logger.LogInformation("User logged in: {Email}", user.Email);

        return Ok(new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            Username = user.Username,
            Name = user.Name,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes)
        });
    }
}
