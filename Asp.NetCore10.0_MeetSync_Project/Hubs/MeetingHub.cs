using MeetSync.Domain.Entities;
using MeetSync.Application.DTOs.Meeting;
using MeetSync.Application.DTOs.Recording;
using MeetSync.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Asp.NetCore10._0_MeetSync_Project.Hubs
{
    public class MeetingHub : Hub
    {
        private readonly IMeetingStateStore _stateStore;
        private readonly IMeetingService _meetingService;
        private readonly IRoomService _roomService;
        private readonly IMeetingRecordingService _recordingService;
        private readonly ITranscriptionService _transcriptionService;
        private readonly IAiSummaryService _aiSummaryService;
        private readonly ILogger<MeetingHub> _logger;


        public MeetingHub(
            IMeetingStateStore stateStore,
            IMeetingService meetingService,
            IRoomService roomService,
            IMeetingRecordingService recordingService,
            ITranscriptionService transcriptionService,
            IAiSummaryService aiSummaryService,
            ILogger<MeetingHub> logger)
        {
            _stateStore = stateStore;
            _meetingService = meetingService;
            _roomService = roomService;
            _recordingService = recordingService;
            _transcriptionService = transcriptionService;
            _aiSummaryService = aiSummaryService;
            _logger = logger;
        }

        private static string NormalizeRoomName(string? roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName)) return string.Empty;
            return RoomName.Key(roomName);
        }

        public async Task<List<ParticipantDto>> JoinRoom(string roomName, string userName, string? password = null)
        {
            var normRoomName = RoomName.Clean(roomName);
            var storeRoomKey = NormalizeRoomName(roomName);

            if (string.IsNullOrWhiteSpace(normRoomName) || string.IsNullOrWhiteSpace(userName))
            {
                _logger.LogWarning("JoinRoom invoked with invalid parameters by connection {ConnectionId}", Context.ConnectionId);
                return new List<ParticipantDto>();
            }

            bool isLocked = await _stateStore.IsRoomLockedAsync(storeRoomKey);
            if (isLocked)
            {
                await Clients.Caller.SendAsync("RoomIsLocked");
                return new List<ParticipantDto>();
            }

            bool isPasswordValid = await _roomService.VerifyPasswordAsync(normRoomName, password);
            if (!isPasswordValid)
            {
                await Clients.Caller.SendAsync("InvalidRoomPassword");
                return new List<ParticipantDto>();
            }

            var canJoin = await _meetingService.CanJoinMeetingAsync(normRoomName);
            if (!canJoin)
            {
                await Clients.Caller.SendAsync("RoomClosedByModerator");
                return new List<ParticipantDto>();
            }

            // Joining SignalR must not create a replacement room if the meeting ended.
            var room = await _roomService.GetRoomByNameAsync(normRoomName);
            if (room == null)
            {
                await Clients.Caller.SendAsync("RoomClosedByModerator");
                return new List<ParticipantDto>();
            }
            if (room.IsLocked)
            {
                await Clients.Caller.SendAsync("RoomIsLocked");
                return new List<ParticipantDto>();
            }
            var activeParticipantsBefore = await _stateStore.GetParticipantsAsync(storeRoomKey);
            bool isFirstParticipant = activeParticipantsBefore.Count == 0;

            if (room.IsLobbyEnabled && !isFirstParticipant)
            {
                await _stateStore.AddWaitingParticipantAsync(storeRoomKey, Context.ConnectionId, userName);
                await Clients.Caller.SendAsync("PlacedInWaitingRoom");

                var waitingList = await _stateStore.GetWaitingParticipantsAsync(storeRoomKey);
                await Clients.Group(storeRoomKey).SendAsync("UserWaitingInLobby", Context.ConnectionId, userName);
                await Clients.Group(storeRoomKey).SendAsync("UpdateWaitingList", waitingList);
                return new List<ParticipantDto>();
            }

            // 1. Ensure SignalR group registration BEFORE state store addition
            await Groups.AddToGroupAsync(Context.ConnectionId, storeRoomKey);

            // 2. Add participant to state store (distinct by Context.ConnectionId)
            bool isModerator = await _stateStore.AddParticipantAsync(storeRoomKey, Context.ConnectionId, userName);
            var updatedParticipants = (await _stateStore.GetParticipantsAsync(storeRoomKey)).ToList();

            _logger.LogInformation("[MeetSync System Log] User {UserName} ({ConnectionId}) joined room '{RoomName}'. Moderator: {IsModerator}, Count: {Count}",
                userName, Context.ConnectionId, storeRoomKey, isModerator, updatedParticipants.Count);

            await Clients.Caller.SendAsync("ModeratorStatus", isModerator);
            await Clients.Caller.SendAsync("RoomLockStatusChanged", room.IsLocked);

            // 3. Broadcast participant list to group
            await Clients.Group(storeRoomKey).SendAsync("UpdateParticipantList", updatedParticipants);
            await Clients.Group(storeRoomKey).SendAsync("UpdateParticipants", updatedParticipants);

            // 4. Notify other participants in group of new arrival
            await Clients.OthersInGroup(storeRoomKey).SendAsync("UserJoined", userName, Context.ConnectionId);

            var currentWaitingList = await _stateStore.GetWaitingParticipantsAsync(storeRoomKey);
            if (currentWaitingList.Count > 0)
            {
                await Clients.Caller.SendAsync("UpdateWaitingList", currentWaitingList);
            }

            return updatedParticipants;
        }

        // --- AI LIVE CAPTIONS & SUMMARY HUB METHODS ---

        public async Task SendTranscriptionChunk(string roomName, string text)
        {
            var normRoomName = NormalizeRoomName(roomName);
            if (string.IsNullOrWhiteSpace(normRoomName) || string.IsNullOrWhiteSpace(text)) return;

            var room = await _roomService.GetRoomByNameAsync(normRoomName);
            if (room == null) return;

            var participants = await _stateStore.GetParticipantsAsync(normRoomName);
            var speaker = participants.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            string speakerName = speaker?.UserName ?? "Participant";

            await _transcriptionService.AddTranscriptChunkAsync(room.Id, speakerName, text);
            await Clients.Group(normRoomName).SendAsync("ReceiveLiveCaption", speakerName, text);
        }

        public async Task GenerateMeetingSummary(string roomName)
        {
            var normRoomName = NormalizeRoomName(roomName);
            if (string.IsNullOrWhiteSpace(normRoomName)) return;

            var modId = await _stateStore.GetModeratorAsync(normRoomName);
            if (modId != Context.ConnectionId) return;

            var room = await _roomService.GetRoomByNameAsync(normRoomName);
            if (room == null) return;

            _logger.LogInformation("AI Summary generation triggered by moderator for room '{RoomName}'", normRoomName);

            var summary = await _aiSummaryService.GenerateSummaryAsync(room.Id);
            await Clients.Group(normRoomName).SendAsync("MeetingSummaryReady", summary);
        }

        // --- RECORDING HUB CONTROLS ---

        public async Task<Guid> StartRecording(string roomName)
        {
            var key = NormalizeRoomName(roomName);
            if (await _stateStore.GetModeratorAsync(key) != Context.ConnectionId)
                throw new HubException("Kaydı yalnızca moderatör başlatabilir.");
            if (Context.Items.ContainsKey("recordingId"))
                throw new HubException("Önce devam eden kaydı durdurun.");
            Guid? userId = Guid.TryParse(Context.GetHttpContext()?.Session.GetString("userId"), out var id) ? id : null;
            var recording = await _recordingService.StartRecordingAsync(new StartRecordingRequestDto(key, userId));
            Context.Items["recordingId"] = recording.Id;
            Context.Items["recordingRoom"] = key;
            await Clients.Group(key).SendAsync("RecordingStarted", recording.Id, Context.ConnectionId);
            return recording.Id;
        }

        private void RequireRecordingOwner(string roomName, Guid recordingId)
        {
            if (!Context.Items.TryGetValue("recordingId", out var owned) ||
                owned is not Guid id || id != recordingId ||
                !Equals(Context.Items["recordingRoom"], NormalizeRoomName(roomName)))
                throw new HubException("Bu kayıt bu bağlantıya ait değil.");
        }

        public async Task StopRecording(string roomName, Guid recordingId, int durationSeconds, bool failed = false)
        {
            RequireRecordingOwner(roomName, recordingId);
            var result = await _recordingService.StopRecordingAsync(new StopRecordingRequestDto(recordingId, durationSeconds, failed));
            Context.Items.Remove("recordingId");
            Context.Items.Remove("recordingRoom");
            await Clients.Group(NormalizeRoomName(roomName)).SendAsync("RecordingStopped", recordingId, result);
        }

        public async Task UploadRecordingChunk(string roomName, Guid recordingId, string chunkBase64)
        {
            RequireRecordingOwner(roomName, recordingId);
            // 12 KiB raw = 16 KiB base64, safely below SignalR's 32 KiB message limit.
            if (string.IsNullOrEmpty(chunkBase64) || chunkBase64.Length > 16 * 1024)
                throw new HubException("Geçersiz kayıt parçası.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(chunkBase64); }
            catch (FormatException) { throw new HubException("Geçersiz kayıt verisi."); }
            await _recordingService.AppendChunkAsync(recordingId, bytes);
        }

        // --- LOBBY MANAGEMENT METHODS ---

        public async Task ApproveParticipant(string roomName, string targetConnectionId)
        {
            var normRoomName = NormalizeRoomName(roomName);
            if (string.IsNullOrWhiteSpace(normRoomName) || string.IsNullOrWhiteSpace(targetConnectionId)) return;

            var modId = await _stateStore.GetModeratorAsync(normRoomName);
            if (modId != Context.ConnectionId) return;

            var approved = await _stateStore.ApproveWaitingParticipantAsync(normRoomName, targetConnectionId);
            if (approved != null)
            {
                await Groups.AddToGroupAsync(targetConnectionId, normRoomName);
                await Clients.Client(targetConnectionId).SendAsync("LobbyApproved");

                var updatedParticipants = await _stateStore.GetParticipantsAsync(normRoomName);
                await Clients.Group(normRoomName).SendAsync("UpdateParticipantList", updatedParticipants);
                await Clients.Group(normRoomName).SendAsync("UpdateParticipants", updatedParticipants);
                await Clients.OthersInGroup(normRoomName).SendAsync("UserJoined", approved.UserName, targetConnectionId);

                var waitingList = await _stateStore.GetWaitingParticipantsAsync(normRoomName);
                await Clients.Group(normRoomName).SendAsync("UpdateWaitingList", waitingList);
            }
        }

        public async Task RejectParticipant(string roomName, string targetConnectionId)
        {
            var normRoomName = NormalizeRoomName(roomName);
            if (string.IsNullOrWhiteSpace(normRoomName) || string.IsNullOrWhiteSpace(targetConnectionId)) return;

            var modId = await _stateStore.GetModeratorAsync(normRoomName);
            if (modId != Context.ConnectionId) return;

            var rejected = await _stateStore.RejectWaitingParticipantAsync(normRoomName, targetConnectionId);
            if (rejected != null)
            {
                await Clients.Client(targetConnectionId).SendAsync("LobbyRejected");
                var waitingList = await _stateStore.GetWaitingParticipantsAsync(normRoomName);
                await Clients.Group(normRoomName).SendAsync("UpdateWaitingList", waitingList);
            }
        }

        public async Task SetRoomLock(string roomName, bool isLocked)
        {
            var normRoomName = NormalizeRoomName(roomName);
            if (string.IsNullOrWhiteSpace(normRoomName)) return;

            var modId = await _stateStore.GetModeratorAsync(normRoomName);
            if (modId != Context.ConnectionId) throw new HubException("Oda kilidini yalnızca moderatör değiştirebilir.");

            if (!await _roomService.SetRoomLockAsync(normRoomName, isLocked))
                throw new HubException("Aktif oda bulunamadı.");
            await _stateStore.SetRoomLockAsync(normRoomName, isLocked);

            await Clients.Group(normRoomName).SendAsync("RoomLockStatusChanged", isLocked);
        }

        // --- REMOTE PARTICIPANT CONTROL METHODS (HOST COMMANDS) ---

        public async Task MuteParticipant(string roomName, string targetConnectionId, string mediaType)
        {
            var normRoomName = NormalizeRoomName(roomName);
            if (string.IsNullOrWhiteSpace(normRoomName) || string.IsNullOrWhiteSpace(targetConnectionId)) return;

            var modId = await _stateStore.GetModeratorAsync(normRoomName);
            if (modId != Context.ConnectionId) return;

            await Clients.Client(targetConnectionId).SendAsync("ParticipantMuted", targetConnectionId, mediaType);
            await Clients.Group(normRoomName).SendAsync("ParticipantMutedStateChanged", targetConnectionId, mediaType);
        }

        public async Task MuteAllParticipants(string roomName, string mediaType)
        {
            var normRoomName = NormalizeRoomName(roomName);
            if (string.IsNullOrWhiteSpace(normRoomName)) return;

            var modId = await _stateStore.GetModeratorAsync(normRoomName);
            if (modId != Context.ConnectionId) return;

            await Clients.OthersInGroup(normRoomName).SendAsync("AllParticipantsMuted", mediaType);
        }

        public async Task KickParticipant(string roomName, string targetConnectionId, string reason)
        {
            var normRoomName = NormalizeRoomName(roomName);
            if (string.IsNullOrWhiteSpace(normRoomName) || string.IsNullOrWhiteSpace(targetConnectionId)) return;

            var modId = await _stateStore.GetModeratorAsync(normRoomName);
            if (modId != Context.ConnectionId) return;

            await Clients.Client(targetConnectionId).SendAsync("ForceKicked", reason ?? "Kicked by meeting moderator.");
            await Groups.RemoveFromGroupAsync(targetConnectionId, normRoomName);
            await _stateStore.RemoveParticipantAsync(targetConnectionId);

            var participants = await _stateStore.GetParticipantsAsync(normRoomName);
            await Clients.Group(normRoomName).SendAsync("UpdateParticipantList", participants);
            await Clients.Group(normRoomName).SendAsync("UpdateParticipants", participants);
            await Clients.Group(normRoomName).SendAsync("UserDisconnected", targetConnectionId);
        }

        // --- EXISTING WEBRTC SIGNALING METHODS ---

        public async Task SendOffer(string offer, string targetId)
        {
            await Clients.Client(targetId).SendAsync("ReceiveOffer", offer, Context.ConnectionId);
        }

        public async Task SendAnswer(string answer, string targetId)
        {
            await Clients.Client(targetId).SendAsync("ReceiveAnswer", answer, Context.ConnectionId);
        }

        public async Task SendIceCandidate(string candidate, string targetId)
        {
            await Clients.Client(targetId).SendAsync("ReceiveIceCandidate", candidate, Context.ConnectionId);
        }

        public async Task SendMessage(string roomName, string userName, string message)
        {
            var normRoomName = NormalizeRoomName(roomName);
            if (string.IsNullOrWhiteSpace(normRoomName) || string.IsNullOrWhiteSpace(message)) return;

            await Clients.Group(normRoomName).SendAsync("ReceiveMessage", userName, message);
        }

        public async Task UpdateUserName(string roomName, string newUserName)
        {
            var normRoomName = NormalizeRoomName(roomName);
            if (string.IsNullOrWhiteSpace(normRoomName) || string.IsNullOrWhiteSpace(newUserName)) return;

            await _stateStore.UpdateParticipantNameAsync(normRoomName, Context.ConnectionId, newUserName);
            var participants = await _stateStore.GetParticipantsAsync(normRoomName);

            await Clients.Group(normRoomName).SendAsync("UserNameUpdated", Context.ConnectionId, newUserName);
            await Clients.Group(normRoomName).SendAsync("UpdateParticipantList", participants);
            await Clients.Group(normRoomName).SendAsync("UpdateParticipants", participants);
        }

        public async Task AssignModerator(string roomName, string newModeratorConnectionId)
        {
            var normRoomName = NormalizeRoomName(roomName);
            if (string.IsNullOrWhiteSpace(normRoomName) || string.IsNullOrWhiteSpace(newModeratorConnectionId)) return;

            await _stateStore.SetModeratorAsync(normRoomName, newModeratorConnectionId);
            var participants = await _stateStore.GetParticipantsAsync(normRoomName);

            _logger.LogInformation("Moderator reassigned in room '{RoomName}' to {NewModeratorConnectionId}", normRoomName, newModeratorConnectionId);

            await Clients.Group(normRoomName).SendAsync("ModeratorChanged", newModeratorConnectionId);
            await Clients.Group(normRoomName).SendAsync("UpdateParticipantList", participants);
            await Clients.Group(normRoomName).SendAsync("UpdateParticipants", participants);
            await Clients.Client(newModeratorConnectionId).SendAsync("ModeratorStatus", true);
        }

        public async Task EndMeeting(string roomName)
        {
            var normRoomName = NormalizeRoomName(roomName);
            if (string.IsNullOrWhiteSpace(normRoomName)) return;

            var modId = await _stateStore.GetModeratorAsync(normRoomName);
            if (modId == Context.ConnectionId)
            {
                _logger.LogInformation("Room '{RoomName}' closed by moderator {ConnectionId}", normRoomName, Context.ConnectionId);

                var room = await _roomService.GetRoomByNameAsync(normRoomName);
                if (room != null) await _roomService.DeactivateRoomAsync(room.Id);
                await Clients.Group(normRoomName).SendAsync("RoomClosedByModerator");
                await _stateStore.RemoveRoomAsync(normRoomName);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (Context.Items.TryGetValue("recordingId", out var owned) && owned is Guid recordingId)
            {
                try
                {
                    var failed = await _recordingService.StopRecordingAsync(new StopRecordingRequestDto(recordingId, 0, true));
                    await Clients.Group((string)Context.Items["recordingRoom"]!).SendAsync("RecordingStopped", recordingId, failed);
                }
                catch (Exception ex) { _logger.LogError(ex, "Could not finalize disconnected recording {RecordingId}", recordingId); }
            }

            var roomName = await _stateStore.GetParticipantRoomAsync(Context.ConnectionId);

            var (removedParticipant, promotedNewMod, newMod, roomIsEmpty) =
                await _stateStore.RemoveParticipantAsync(Context.ConnectionId);

            if (removedParticipant != null && !string.IsNullOrEmpty(roomName))
            {
                var normRoomName = NormalizeRoomName(roomName);
                _logger.LogInformation("User {UserName} ({ConnectionId}) disconnected from room '{RoomName}'.",
                    removedParticipant.UserName, Context.ConnectionId, normRoomName);

                if (promotedNewMod && newMod != null)
                {
                    _logger.LogInformation("Promoted new moderator {NewModId} for room '{RoomName}'.", newMod.ConnectionId, normRoomName);
                    await Clients.Group(normRoomName).SendAsync("ModeratorChanged", newMod.ConnectionId);
                    await Clients.Client(newMod.ConnectionId).SendAsync("ModeratorStatus", true);
                }

                if (!roomIsEmpty)
                {
                    var remainingParticipants = await _stateStore.GetParticipantsAsync(normRoomName);
                    await Clients.Group(normRoomName).SendAsync("UpdateParticipantList", remainingParticipants);
                    await Clients.Group(normRoomName).SendAsync("UpdateParticipants", remainingParticipants);
                }

                await Clients.Group(normRoomName).SendAsync("UserDisconnected", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
