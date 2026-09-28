using MeetSync.Application.DTOs.Auth;

namespace MeetSync.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<UserResponseDto?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
