namespace MeetSync.Application.DTOs.Auth;

public record UserResponseDto(
    Guid Id,
    string Email,
    string DisplayName,
    DateTime CreatedAt
);
