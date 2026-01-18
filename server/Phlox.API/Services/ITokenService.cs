using Phlox.API.Entities;

namespace Phlox.API.Services;

public interface ITokenService
{
    string GenerateToken(UserEntity user);
}
