using System.Collections.Concurrent;
using MeetSync.Domain.Entities;
using MeetSync.Application.DTOs.Meeting;
using MeetSync.Application.Interfaces;

namespace MeetSync.Infrastructure.Services.State;

public class InMemoryMeetingStateStore : IMeetingStateStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ParticipantDto>> _roomParticipants = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ParticipantDto>> _waitingParticipants = new();
    private readonly ConcurrentDictionary<string, string> _roomModerators = new();
    private readonly ConcurrentDictionary<string, string> _connectionRooms = new();
    private readonly ConcurrentDictionary<string, bool> _roomLocks = new();


    private static string NormalizeRoomName(string? roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName)) return string.Empty;
        return RoomName.Key(roomName);
    }

    public Task<bool> AddParticipantAsync(string roomName, string connectionId, string userName, CancellationToken cancellationToken = default)
    {
        var normRoom = NormalizeRoomName(roomName);
        var roomMap = _roomParticipants.GetOrAdd(normRoom, _ => new ConcurrentDictionary<string, ParticipantDto>());
        _connectionRooms[connectionId] = normRoom;

        bool isModerator;
        lock (roomMap)
        {
            isModerator = roomMap.IsEmpty;
            if (isModerator)
            {
                _roomModerators[normRoom] = connectionId;
            }

            var participant = new ParticipantDto(connectionId, userName, isModerator);
            roomMap[connectionId] = participant;
        }

        return Task.FromResult(isModerator);
    }

    public Task<(ParticipantDto? RemovedParticipant, bool PromotedNewMod, ParticipantDto? NewMod, bool RoomIsEmpty)> RemoveParticipantAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        // Check if connection was in waiting room
        foreach (var waitingRoom in _waitingParticipants)
        {
            waitingRoom.Value.TryRemove(connectionId, out _);
        }

        if (!_connectionRooms.TryRemove(connectionId, out var roomName) ||
            !_roomParticipants.TryGetValue(roomName, out var roomMap))
        {
            return Task.FromResult<(ParticipantDto?, bool, ParticipantDto?, bool)>((null, false, null, false));
        }

        ParticipantDto? removedParticipant = null;
        bool promotedNewMod = false;
        ParticipantDto? newMod = null;
        bool roomIsEmpty = false;

        lock (roomMap)
        {
            if (roomMap.TryRemove(connectionId, out removedParticipant))
            {
                if (removedParticipant.IsModerator && !roomMap.IsEmpty)
                {
                    promotedNewMod = true;
                    var firstEntry = roomMap.Values.First();
                    newMod = firstEntry with { IsModerator = true };
                    roomMap[newMod.ConnectionId] = newMod;
                    _roomModerators[roomName] = newMod.ConnectionId;
                }

                if (roomMap.IsEmpty)
                {
                    roomIsEmpty = true;
                    _roomParticipants.TryRemove(roomName, out _);
                    _roomModerators.TryRemove(roomName, out _);
                    _waitingParticipants.TryRemove(roomName, out _);
                    _roomLocks.TryRemove(roomName, out _);
                }
            }
        }

        return Task.FromResult((removedParticipant, promotedNewMod, newMod, roomIsEmpty));
    }

    public Task<IReadOnlyList<ParticipantDto>> GetParticipantsAsync(string roomName, CancellationToken cancellationToken = default)
    {
        var normRoom = NormalizeRoomName(roomName);
        if (_roomParticipants.TryGetValue(normRoom, out var roomMap))
        {
            return Task.FromResult<IReadOnlyList<ParticipantDto>>(roomMap.Values.ToList());
        }

        return Task.FromResult<IReadOnlyList<ParticipantDto>>(Array.Empty<ParticipantDto>());
    }

    public Task<string?> GetParticipantRoomAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        _connectionRooms.TryGetValue(connectionId, out var roomName);
        return Task.FromResult(roomName);
    }

    public Task SetModeratorAsync(string roomName, string newModeratorConnectionId, CancellationToken cancellationToken = default)
    {
        var normRoom = NormalizeRoomName(roomName);
        if (_roomParticipants.TryGetValue(normRoom, out var roomMap))
        {
            lock (roomMap)
            {
                foreach (var kvp in roomMap.ToList())
                {
                    var updated = kvp.Value with { IsModerator = (kvp.Key == newModeratorConnectionId) };
                    roomMap[kvp.Key] = updated;
                }
                _roomModerators[normRoom] = newModeratorConnectionId;
            }
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetModeratorAsync(string roomName, CancellationToken cancellationToken = default)
    {
        var normRoom = NormalizeRoomName(roomName);
        _roomModerators.TryGetValue(normRoom, out var modId);
        return Task.FromResult(modId);
    }

    public Task UpdateParticipantNameAsync(string roomName, string connectionId, string newUserName, CancellationToken cancellationToken = default)
    {
        var normRoom = NormalizeRoomName(roomName);
        if (_roomParticipants.TryGetValue(normRoom, out var roomMap))
        {
            if (roomMap.TryGetValue(connectionId, out var existing))
            {
                roomMap[connectionId] = existing with { UserName = newUserName };
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> RemoveRoomAsync(string roomName, CancellationToken cancellationToken = default)
    {
        var normRoom = NormalizeRoomName(roomName);
        var connectionIds = new List<string>();

        if (_roomParticipants.TryRemove(normRoom, out var roomMap))
        {
            _roomModerators.TryRemove(normRoom, out _);
            _waitingParticipants.TryRemove(normRoom, out _);
            _roomLocks.TryRemove(normRoom, out _);

            lock (roomMap)
            {
                foreach (var connId in roomMap.Keys)
                {
                    _connectionRooms.TryRemove(connId, out _);
                    connectionIds.Add(connId);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(connectionIds);
    }

    // Lobby / Waiting Room Implementations
    public Task AddWaitingParticipantAsync(string roomName, string connectionId, string userName, CancellationToken cancellationToken = default)
    {
        var normRoom = NormalizeRoomName(roomName);
        var waitingMap = _waitingParticipants.GetOrAdd(normRoom, _ => new ConcurrentDictionary<string, ParticipantDto>());
        waitingMap[connectionId] = new ParticipantDto(connectionId, userName, false);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ParticipantDto>> GetWaitingParticipantsAsync(string roomName, CancellationToken cancellationToken = default)
    {
        var normRoom = NormalizeRoomName(roomName);
        if (_waitingParticipants.TryGetValue(normRoom, out var waitingMap))
        {
            return Task.FromResult<IReadOnlyList<ParticipantDto>>(waitingMap.Values.ToList());
        }

        return Task.FromResult<IReadOnlyList<ParticipantDto>>(Array.Empty<ParticipantDto>());
    }

    public Task<ParticipantDto?> ApproveWaitingParticipantAsync(string roomName, string connectionId, CancellationToken cancellationToken = default)
    {
        var normRoom = NormalizeRoomName(roomName);
        if (_waitingParticipants.TryGetValue(normRoom, out var waitingMap))
        {
            if (waitingMap.TryRemove(connectionId, out var participant))
            {
                var roomMap = _roomParticipants.GetOrAdd(normRoom, _ => new ConcurrentDictionary<string, ParticipantDto>());
                _connectionRooms[connectionId] = normRoom;
                roomMap[connectionId] = participant;
                return Task.FromResult<ParticipantDto?>(participant);
            }
        }

        return Task.FromResult<ParticipantDto?>(null);
    }

    public Task<ParticipantDto?> RejectWaitingParticipantAsync(string roomName, string connectionId, CancellationToken cancellationToken = default)
    {
        var normRoom = NormalizeRoomName(roomName);
        if (_waitingParticipants.TryGetValue(normRoom, out var waitingMap))
        {
            if (waitingMap.TryRemove(connectionId, out var participant))
            {
                return Task.FromResult<ParticipantDto?>(participant);
            }
        }

        return Task.FromResult<ParticipantDto?>(null);
    }

    // Room Lock Implementations
    public Task SetRoomLockAsync(string roomName, bool isLocked, CancellationToken cancellationToken = default)
    {
        var normRoom = NormalizeRoomName(roomName);
        _roomLocks[normRoom] = isLocked;
        return Task.CompletedTask;
    }

    public Task<bool> IsRoomLockedAsync(string roomName, CancellationToken cancellationToken = default)
    {
        var normRoom = NormalizeRoomName(roomName);
        _roomLocks.TryGetValue(normRoom, out var isLocked);
        return Task.FromResult(isLocked);
    }
}
