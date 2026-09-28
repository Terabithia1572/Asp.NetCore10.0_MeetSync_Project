namespace MeetSync.Application.DTOs.Auth;

public record AuthResponseDto(
    bool Success,
    string? Message = null,
    UserResponseDto? User = null
);
