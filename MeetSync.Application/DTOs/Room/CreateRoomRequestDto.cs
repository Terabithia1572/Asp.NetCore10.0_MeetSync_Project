namespace MeetSync.Application.DTOs.Room;

public record CreateRoomRequestDto(
    string Name,
    Guid? CreatedBy = null,
    bool IsLobbyEnabled = false,
    string? Password = null
);
