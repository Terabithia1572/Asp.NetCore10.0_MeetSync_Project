namespace MeetSync.Application.DTOs.Room;

public record RoomResponseDto(
    Guid Id,
    string Name,
    Guid CreatedBy,
    DateTime CreatedAt,
    bool IsActive,
    bool IsLobbyEnabled,
    bool IsLocked,
    bool HasPassword
);
