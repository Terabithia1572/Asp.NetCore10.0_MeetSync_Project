namespace MeetSync.Application.DTOs.Room;

public record JoinRoomRequestDto(
    string RoomName,
    string? UserName = null,
    string? Password = null,
    Guid? CreatedBy = null
);
