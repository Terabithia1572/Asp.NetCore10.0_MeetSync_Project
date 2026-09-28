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

        public async Task JoinRoom(string roomName, string userName, string? password = null)
        {
            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(userName))
            {
                _logger.LogWarning("JoinRoom invoked with invalid parameters by connection {ConnectionId}", Context.ConnectionId);
                return;
            }

            bool isLocked = await _stateStore.IsRoomLockedAsync(roomName);
            if (isLocked)
            {
                await Clients.Caller.SendAsync("RoomIsLocked");
                return;
            }

            bool isPasswordValid = await _roomService.VerifyPasswordAsync(roomName, password);
            if (!isPasswordValid)
            {
                await Clients.Caller.SendAsync("InvalidRoomPassword");
                return;
            }

            var canJoin = await _meetingService.CanJoinMeetingAsync(roomName);
            if (!canJoin)
            {
                await Clients.Caller.SendAsync("RoomClosedByModerator");
                return;
            }

            var room = await _roomService.GetRoomByNameAsync(roomName);
            var activeParticipants = await _stateStore.GetParticipantsAsync(roomName);
            bool isFirstParticipant = activeParticipants.Count == 0;

            if (room != null && room.IsLobbyEnabled && !isFirstParticipant)
            {
                await _stateStore.AddWaitingParticipantAsync(roomName, Context.ConnectionId, userName);
                await Clients.Caller.SendAsync("PlacedInWaitingRoom");

                var waitingList = await _stateStore.GetWaitingParticipantsAsync(roomName);
                await Clients.Group(roomName).SendAsync("UserWaitingInLobby", Context.ConnectionId, userName);
                await Clients.Group(roomName).SendAsync("UpdateWaitingList", waitingList);
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
            bool isModerator = await _stateStore.AddParticipantAsync(roomName, Context.ConnectionId, userName);
            var updatedParticipants = await _stateStore.GetParticipantsAsync(roomName);

            _logger.LogInformation("User {UserName} ({ConnectionId}) joined room '{RoomName}'. Moderator: {IsModerator}",
                userName, Context.ConnectionId, roomName, isModerator);

            await Clients.Caller.SendAsync("ModeratorStatus", isModerator);
            await Clients.Group(roomName).SendAsync("UpdateParticipants", updatedParticipants);
            await Clients.OthersInGroup(roomName).SendAsync("UserJoined", userName, Context.ConnectionId);

            var currentWaitingList = await _stateStore.GetWaitingParticipantsAsync(roomName);
            if (currentWaitingList.Count > 0)
            {
                await Clients.Caller.SendAsync("UpdateWaitingList", currentWaitingList);
            }
        }

        // --- AI LIVE CAPTIONS & SUMMARY HUB METHODS ---

        public async Task SendTranscriptionChunk(string roomName, string text)
        {
            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(text)) return;

            var room = await _roomService.GetRoomByNameAsync(roomName);
            if (room == null) return;

            var participants = await _stateStore.GetParticipantsAsync(roomName);
            var speaker = participants.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            string speakerName = speaker?.UserName ?? "Participant";

            await _transcriptionService.AddTranscriptChunkAsync(room.Id, speakerName, text);
            await Clients.Group(roomName).SendAsync("ReceiveLiveCaption", speakerName, text);
        }

        public async Task GenerateMeetingSummary(string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName)) return;

            var modId = await _stateStore.GetModeratorAsync(roomName);
            if (modId != Context.ConnectionId) return;

            var room = await _roomService.GetRoomByNameAsync(roomName);
            if (room == null) return;

            _logger.LogInformation("AI Summary generation triggered by moderator for room '{RoomName}'", roomName);

            var summary = await _aiSummaryService.GenerateSummaryAsync(room.Id);
            await Clients.Group(roomName).SendAsync("MeetingSummaryReady", summary);
        }

        // --- RECORDING HUB CONTROLS ---

        public async Task StartRecording(string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName)) return;

            var modId = await _stateStore.GetModeratorAsync(roomName);
            if (modId != Context.ConnectionId) return;

            var recording = await _recordingService.StartRecordingAsync(new StartRecordingRequestDto(roomName));
            await Clients.Group(roomName).SendAsync("RecordingStarted", recording.Id);
        }

        public async Task StopRecording(string roomName, Guid recordingId, int durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(roomName)) return;

            var modId = await _stateStore.GetModeratorAsync(roomName);
            if (modId != Context.ConnectionId) return;

            var result = await _recordingService.StopRecordingAsync(new StopRecordingRequestDto(recordingId, durationSeconds));
            await Clients.Group(roomName).SendAsync("RecordingStopped", recordingId, result);
        }

        public async Task UploadRecordingChunk(string roomName, Guid recordingId, string chunkBase64)
        {
            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(chunkBase64)) return;

            var modId = await _stateStore.GetModeratorAsync(roomName);
            if (modId != Context.ConnectionId) return;

            byte[] chunkBytes = Convert.FromBase64String(chunkBase64);
            await _recordingService.AppendChunkAsync(recordingId, chunkBytes);
        }

        // --- LOBBY MANAGEMENT METHODS ---

        public async Task ApproveParticipant(string roomName, string targetConnectionId)
        {
            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(targetConnectionId)) return;

            var modId = await _stateStore.GetModeratorAsync(roomName);
            if (modId != Context.ConnectionId) return;

            var approved = await _stateStore.ApproveWaitingParticipantAsync(roomName, targetConnectionId);
            if (approved != null)
            {
                await Groups.AddToGroupAsync(targetConnectionId, roomName);
                await Clients.Client(targetConnectionId).SendAsync("LobbyApproved");

                var updatedParticipants = await _stateStore.GetParticipantsAsync(roomName);
                await Clients.Group(roomName).SendAsync("UpdateParticipants", updatedParticipants);
                await Clients.OthersInGroup(roomName).SendAsync("UserJoined", approved.UserName, targetConnectionId);

                var waitingList = await _stateStore.GetWaitingParticipantsAsync(roomName);
                await Clients.Group(roomName).SendAsync("UpdateWaitingList", waitingList);
            }
        }

        public async Task RejectParticipant(string roomName, string targetConnectionId)
        {
            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(targetConnectionId)) return;

            var modId = await _stateStore.GetModeratorAsync(roomName);
            if (modId != Context.ConnectionId) return;

            var rejected = await _stateStore.RejectWaitingParticipantAsync(roomName, targetConnectionId);
            if (rejected != null)
            {
                await Clients.Client(targetConnectionId).SendAsync("LobbyRejected");
                var waitingList = await _stateStore.GetWaitingParticipantsAsync(roomName);
                await Clients.Group(roomName).SendAsync("UpdateWaitingList", waitingList);
            }
        }

        public async Task SetRoomLock(string roomName, bool isLocked)
        {
            if (string.IsNullOrWhiteSpace(roomName)) return;

            var modId = await _stateStore.GetModeratorAsync(roomName);
            if (modId != Context.ConnectionId) return;

            await _stateStore.SetRoomLockAsync(roomName, isLocked);
            await _roomService.SetRoomLockAsync(roomName, isLocked);

            await Clients.Group(roomName).SendAsync("RoomLockStatusChanged", isLocked);
        }

        // --- REMOTE PARTICIPANT CONTROL METHODS (HOST COMMANDS) ---

        public async Task MuteParticipant(string roomName, string targetConnectionId, string mediaType)
        {
            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(targetConnectionId)) return;

            var modId = await _stateStore.GetModeratorAsync(roomName);
            if (modId != Context.ConnectionId) return;

            await Clients.Client(targetConnectionId).SendAsync("ParticipantMuted", targetConnectionId, mediaType);
            await Clients.Group(roomName).SendAsync("ParticipantMutedStateChanged", targetConnectionId, mediaType);
        }

        public async Task MuteAllParticipants(string roomName, string mediaType)
        {
            if (string.IsNullOrWhiteSpace(roomName)) return;

            var modId = await _stateStore.GetModeratorAsync(roomName);
            if (modId != Context.ConnectionId) return;

            await Clients.OthersInGroup(roomName).SendAsync("AllParticipantsMuted", mediaType);
        }

        public async Task KickParticipant(string roomName, string targetConnectionId, string reason)
        {
            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(targetConnectionId)) return;

            var modId = await _stateStore.GetModeratorAsync(roomName);
            if (modId != Context.ConnectionId) return;

            await Clients.Client(targetConnectionId).SendAsync("ForceKicked", reason ?? "Kicked by meeting moderator.");
            await Groups.RemoveFromGroupAsync(targetConnectionId, roomName);
            await _stateStore.RemoveParticipantAsync(targetConnectionId);

            var participants = await _stateStore.GetParticipantsAsync(roomName);
            await Clients.Group(roomName).SendAsync("UpdateParticipants", participants);
            await Clients.Group(roomName).SendAsync("UserDisconnected", targetConnectionId);
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
            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(message)) return;

            await Clients.Group(roomName).SendAsync("ReceiveMessage", userName, message);
        }

        public async Task UpdateUserName(string roomName, string newUserName)
        {
            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(newUserName)) return;

            await _stateStore.UpdateParticipantNameAsync(roomName, Context.ConnectionId, newUserName);
            var participants = await _stateStore.GetParticipantsAsync(roomName);

            await Clients.Group(roomName).SendAsync("UserNameUpdated", Context.ConnectionId, newUserName);
            await Clients.Group(roomName).SendAsync("UpdateParticipants", participants);
        }

        public async Task AssignModerator(string roomName, string newModeratorConnectionId)
        {
            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(newModeratorConnectionId)) return;

            await _stateStore.SetModeratorAsync(roomName, newModeratorConnectionId);
            var participants = await _stateStore.GetParticipantsAsync(roomName);

            _logger.LogInformation("Moderator reassigned in room '{RoomName}' to {NewModeratorConnectionId}", roomName, newModeratorConnectionId);

            await Clients.Group(roomName).SendAsync("ModeratorChanged", newModeratorConnectionId);
            await Clients.Group(roomName).SendAsync("UpdateParticipants", participants);
            await Clients.Client(newModeratorConnectionId).SendAsync("ModeratorStatus", true);
        }

        public async Task EndMeeting(string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName)) return;

            var modId = await _stateStore.GetModeratorAsync(roomName);
            if (modId == Context.ConnectionId)
            {
                _logger.LogInformation("Room '{RoomName}' closed by moderator {ConnectionId}", roomName, Context.ConnectionId);

                await Clients.Group(roomName).SendAsync("RoomClosedByModerator");
                await _stateStore.RemoveRoomAsync(roomName);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var roomName = await _stateStore.GetParticipantRoomAsync(Context.ConnectionId);

            var (removedParticipant, promotedNewMod, newMod, roomIsEmpty) =
                await _stateStore.RemoveParticipantAsync(Context.ConnectionId);

            if (removedParticipant != null && !string.IsNullOrEmpty(roomName))
            {
                _logger.LogInformation("User {UserName} ({ConnectionId}) disconnected from room '{RoomName}'.",
                    removedParticipant.UserName, Context.ConnectionId, roomName);

                if (promotedNewMod && newMod != null)
                {
                    _logger.LogInformation("Promoted new moderator {NewModId} for room '{RoomName}'.", newMod.ConnectionId, roomName);
                    await Clients.Group(roomName).SendAsync("ModeratorChanged", newMod.ConnectionId);
                    await Clients.Client(newMod.ConnectionId).SendAsync("ModeratorStatus", true);
                }

                if (!roomIsEmpty)
                {
                    var remainingParticipants = await _stateStore.GetParticipantsAsync(roomName);
                    await Clients.Group(roomName).SendAsync("UpdateParticipants", remainingParticipants);
                }

                await Clients.Group(roomName).SendAsync("UserDisconnected", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}