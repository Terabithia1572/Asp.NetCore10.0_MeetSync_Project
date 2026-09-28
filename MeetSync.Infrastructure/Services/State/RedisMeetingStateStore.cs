using System.Text.Json;
using MeetSync.Application.DTOs.Meeting;
using MeetSync.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace MeetSync.Infrastructure.Services.State;

public class RedisMeetingStateStore : IMeetingStateStore
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisMeetingStateStore> _logger;
    private readonly InMemoryMeetingStateStore _fallbackStore;

    private static readonly DistributedCacheEntryOptions DefaultCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
    };

    public RedisMeetingStateStore(IDistributedCache cache, ILogger<RedisMeetingStateStore> logger)
    {
        _cache = cache;
        _logger = logger;
        _fallbackStore = new InMemoryMeetingStateStore();
    }

    private static string GetRoomKey(string roomName) => $"meetsync:room:{roomName}:participants";
    private static string GetWaitingKey(string roomName) => $"meetsync:room:{roomName}:waiting";
    private static string GetConnKey(string connectionId) => $"meetsync:conn:{connectionId}:room";
    private static string GetLockKey(string roomName) => $"meetsync:room:{roomName}:lock";

    public async Task<bool> AddParticipantAsync(string roomName, string connectionId, string userName, CancellationToken cancellationToken = default)
    {
        try
        {
            var participants = (await GetParticipantsAsync(roomName, cancellationToken)).ToList();
            bool isModerator = participants.Count == 0;

            var newParticipant = new ParticipantDto(connectionId, userName, isModerator);
            participants.RemoveAll(p => p.ConnectionId == connectionId);
            participants.Add(newParticipant);

            await _cache.SetStringAsync(GetRoomKey(roomName), JsonSerializer.Serialize(participants), DefaultCacheOptions, cancellationToken);
            await _cache.SetStringAsync(GetConnKey(connectionId), roomName, DefaultCacheOptions, cancellationToken);

            return isModerator;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache failure during AddParticipantAsync. Falling back to in-memory store.");
            return await _fallbackStore.AddParticipantAsync(roomName, connectionId, userName, cancellationToken);
        }
    }

    public async Task<(ParticipantDto? RemovedParticipant, bool PromotedNewMod, ParticipantDto? NewMod, bool RoomIsEmpty)> RemoveParticipantAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var roomName = await GetParticipantRoomAsync(connectionId, cancellationToken);
            if (string.IsNullOrEmpty(roomName))
            {
                return (null, false, null, false);
            }

            await _cache.RemoveAsync(GetConnKey(connectionId), cancellationToken);

            var participants = (await GetParticipantsAsync(roomName, cancellationToken)).ToList();
            var removed = participants.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (removed == null)
            {
                return (null, false, null, false);
            }

            participants.Remove(removed);

            bool promotedNewMod = false;
            ParticipantDto? newMod = null;
            bool roomIsEmpty = participants.Count == 0;

            if (removed.IsModerator && !roomIsEmpty)
            {
                promotedNewMod = true;
                var first = participants[0];
                newMod = first with { IsModerator = true };
                participants[0] = newMod;
            }

            if (roomIsEmpty)
            {
                await _cache.RemoveAsync(GetRoomKey(roomName), cancellationToken);
                await _cache.RemoveAsync(GetWaitingKey(roomName), cancellationToken);
                await _cache.RemoveAsync(GetLockKey(roomName), cancellationToken);
            }
            else
            {
                await _cache.SetStringAsync(GetRoomKey(roomName), JsonSerializer.Serialize(participants), DefaultCacheOptions, cancellationToken);
            }

            return (removed, promotedNewMod, newMod, roomIsEmpty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache failure during RemoveParticipantAsync. Falling back to in-memory store.");
            return await _fallbackStore.RemoveParticipantAsync(connectionId, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ParticipantDto>> GetParticipantsAsync(string roomName, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _cache.GetStringAsync(GetRoomKey(roomName), cancellationToken);
            if (string.IsNullOrEmpty(json)) return Array.Empty<ParticipantDto>();
            var list = JsonSerializer.Deserialize<List<ParticipantDto>>(json);
            return list ?? (IReadOnlyList<ParticipantDto>)Array.Empty<ParticipantDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache failure during GetParticipantsAsync. Falling back to in-memory store.");
            return await _fallbackStore.GetParticipantsAsync(roomName, cancellationToken);
        }
    }

    public async Task<string?> GetParticipantRoomAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _cache.GetStringAsync(GetConnKey(connectionId), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache failure during GetParticipantRoomAsync. Falling back to in-memory store.");
            return await _fallbackStore.GetParticipantRoomAsync(connectionId, cancellationToken);
        }
    }

    public async Task SetModeratorAsync(string roomName, string newModeratorConnectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var participants = (await GetParticipantsAsync(roomName, cancellationToken)).ToList();
            for (int i = 0; i < participants.Count; i++)
            {
                var p = participants[i];
                participants[i] = p with { IsModerator = (p.ConnectionId == newModeratorConnectionId) };
            }

            await _cache.SetStringAsync(GetRoomKey(roomName), JsonSerializer.Serialize(participants), DefaultCacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache failure during SetModeratorAsync. Falling back to in-memory store.");
            await _fallbackStore.SetModeratorAsync(roomName, newModeratorConnectionId, cancellationToken);
        }
    }

    public async Task<string?> GetModeratorAsync(string roomName, CancellationToken cancellationToken = default)
    {
        try
        {
            var participants = await GetParticipantsAsync(roomName, cancellationToken);
            return participants.FirstOrDefault(p => p.IsModerator)?.ConnectionId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache failure during GetModeratorAsync. Falling back to in-memory store.");
            return await _fallbackStore.GetModeratorAsync(roomName, cancellationToken);
        }
    }

    public async Task UpdateParticipantNameAsync(string roomName, string connectionId, string newUserName, CancellationToken cancellationToken = default)
    {
        try
        {
            var participants = (await GetParticipantsAsync(roomName, cancellationToken)).ToList();
            var index = participants.FindIndex(p => p.ConnectionId == connectionId);
            if (index >= 0)
            {
                participants[index] = participants[index] with { UserName = newUserName };
                await _cache.SetStringAsync(GetRoomKey(roomName), JsonSerializer.Serialize(participants), DefaultCacheOptions, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache failure during UpdateParticipantNameAsync. Falling back to in-memory store.");
            await _fallbackStore.UpdateParticipantNameAsync(roomName, connectionId, newUserName, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<string>> RemoveRoomAsync(string roomName, CancellationToken cancellationToken = default)
    {
        try
        {
            var participants = await GetParticipantsAsync(roomName, cancellationToken);
            var connectionIds = participants.Select(p => p.ConnectionId).ToList();

            foreach (var connId in connectionIds)
            {
                await _cache.RemoveAsync(GetConnKey(connId), cancellationToken);
            }

            await _cache.RemoveAsync(GetRoomKey(roomName), cancellationToken);
            await _cache.RemoveAsync(GetWaitingKey(roomName), cancellationToken);
            await _cache.RemoveAsync(GetLockKey(roomName), cancellationToken);

            return connectionIds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache failure during RemoveRoomAsync. Falling back to in-memory store.");
            return await _fallbackStore.RemoveRoomAsync(roomName, cancellationToken);
        }
    }

    public async Task AddWaitingParticipantAsync(string roomName, string connectionId, string userName, CancellationToken cancellationToken = default)
    {
        try
        {
            var waiting = (await GetWaitingParticipantsAsync(roomName, cancellationToken)).ToList();
            waiting.RemoveAll(w => w.ConnectionId == connectionId);
            waiting.Add(new ParticipantDto(connectionId, userName, false));

            await _cache.SetStringAsync(GetWaitingKey(roomName), JsonSerializer.Serialize(waiting), DefaultCacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache failure during AddWaitingParticipantAsync. Falling back to in-memory store.");
            await _fallbackStore.AddWaitingParticipantAsync(roomName, connectionId, userName, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ParticipantDto>> GetWaitingParticipantsAsync(string roomName, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _cache.GetStringAsync(GetWaitingKey(roomName), cancellationToken);
            if (string.IsNullOrEmpty(json)) return Array.Empty<ParticipantDto>();
            var list = JsonSerializer.Deserialize<List<ParticipantDto>>(json);
            return list ?? (IReadOnlyList<ParticipantDto>)Array.Empty<ParticipantDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache failure during GetWaitingParticipantsAsync. Falling back to in-memory store.");
            return await _fallbackStore.GetWaitingParticipantsAsync(roomName, cancellationToken);
        }
    }

    public async Task<ParticipantDto?> ApproveWaitingParticipantAsync(string roomName, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var waiting = (await GetWaitingParticipantsAsync(roomName, cancellationToken)).ToList();
            var participant = waiting.FirstOrDefault(w => w.ConnectionId == connectionId);
            if (participant != null)
            {
                waiting.Remove(participant);
                await _cache.SetStringAsync(GetWaitingKey(roomName), JsonSerializer.Serialize(waiting), DefaultCacheOptions, cancellationToken);
                await AddParticipantAsync(roomName, connectionId, participant.UserName, cancellationToken);
                return participant;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache failure during ApproveWaitingParticipantAsync. Falling back to in-memory store.");
            return await _fallbackStore.ApproveWaitingParticipantAsync(roomName, connectionId, cancellationToken);
        }
    }

    public async Task<ParticipantDto?> RejectWaitingParticipantAsync(string roomName, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var waiting = (await GetWaitingParticipantsAsync(roomName, cancellationToken)).ToList();
            var participant = waiting.FirstOrDefault(w => w.ConnectionId == connectionId);
            if (participant != null)
            {
                waiting.Remove(participant);
                await _cache.SetStringAsync(GetWaitingKey(roomName), JsonSerializer.Serialize(waiting), DefaultCacheOptions, cancellationToken);
                return participant;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache failure during RejectWaitingParticipantAsync. Falling back to in-memory store.");
            return await _fallbackStore.RejectWaitingParticipantAsync(roomName, connectionId, cancellationToken);
        }
    }

    public async Task SetRoomLockAsync(string roomName, bool isLocked, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.SetStringAsync(GetLockKey(roomName), isLocked.ToString(), DefaultCacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache failure during SetRoomLockAsync. Falling back to in-memory store.");
            await _fallbackStore.SetRoomLockAsync(roomName, isLocked, cancellationToken);
        }
    }

    public async Task<bool> IsRoomLockedAsync(string roomName, CancellationToken cancellationToken = default)
    {
        try
        {
            var val = await _cache.GetStringAsync(GetLockKey(roomName), cancellationToken);
            return bool.TryParse(val, out var isLocked) && isLocked;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache failure during IsRoomLockedAsync. Falling back to in-memory store.");
            return await _fallbackStore.IsRoomLockedAsync(roomName, cancellationToken);
        }
    }
}
