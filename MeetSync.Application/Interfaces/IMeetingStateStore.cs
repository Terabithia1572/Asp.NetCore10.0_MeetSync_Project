using MeetSync.Application.DTOs.Meeting;

namespace MeetSync.Application.Interfaces;

public interface IMeetingStateStore
{
    Task<bool> AddParticipantAsync(string roomName, string connectionId, string userName, CancellationToken cancellationToken = default);
    Task<(ParticipantDto? RemovedParticipant, bool PromotedNewMod, ParticipantDto? NewMod, bool RoomIsEmpty)> RemoveParticipantAsync(string connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParticipantDto>> GetParticipantsAsync(string roomName, CancellationToken cancellationToken = default);
    Task<string?> GetParticipantRoomAsync(string connectionId, CancellationToken cancellationToken = default);
    Task SetModeratorAsync(string roomName, string newModeratorConnectionId, CancellationToken cancellationToken = default);
    Task<string?> GetModeratorAsync(string roomName, CancellationToken cancellationToken = default);
    Task UpdateParticipantNameAsync(string roomName, string connectionId, string newUserName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> RemoveRoomAsync(string roomName, CancellationToken cancellationToken = default);

    // Waiting Room / Lobby Methods
    Task AddWaitingParticipantAsync(string roomName, string connectionId, string userName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParticipantDto>> GetWaitingParticipantsAsync(string roomName, CancellationToken cancellationToken = default);
    Task<ParticipantDto?> ApproveWaitingParticipantAsync(string roomName, string connectionId, CancellationToken cancellationToken = default);
    Task<ParticipantDto?> RejectWaitingParticipantAsync(string roomName, string connectionId, CancellationToken cancellationToken = default);

    // Room Lock Methods
    Task SetRoomLockAsync(string roomName, bool isLocked, CancellationToken cancellationToken = default);
    Task<bool> IsRoomLockedAsync(string roomName, CancellationToken cancellationToken = default);
}
