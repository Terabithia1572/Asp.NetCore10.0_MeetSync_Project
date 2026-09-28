using MeetSync.Application.DTOs.Room;

namespace MeetSync.Application.Interfaces;

public interface IMeetingService
{
    Task<RoomResponseDto> PrepareMeetingRoomAsync(JoinRoomRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> CanJoinMeetingAsync(string roomName, CancellationToken cancellationToken = default);
}
