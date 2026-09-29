using MeetSync.Application.DTOs.Room;

namespace MeetSync.Application.Interfaces;

public interface IRoomService
{
    Task<RoomResponseDto> CreateRoomAsync(CreateRoomRequestDto request, CancellationToken cancellationToken = default);
    Task<RoomResponseDto> GetOrCreateRoomAsync(string name, Guid? createdBy = null, CancellationToken cancellationToken = default);
    Task<RoomResponseDto> GetOrCreateRoomAsync(CreateRoomRequestDto request, CancellationToken cancellationToken = default);
    Task<RoomResponseDto?> GetRoomByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RoomResponseDto?> GetRoomByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> DeactivateRoomAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SetRoomLockAsync(string name, bool isLocked, CancellationToken cancellationToken = default);
    Task<bool> VerifyPasswordAsync(string name, string? password, CancellationToken cancellationToken = default);
}
