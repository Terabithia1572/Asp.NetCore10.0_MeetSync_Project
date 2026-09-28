namespace MeetSync.Application.DTOs.Meeting;

public record ParticipantDto(
    string ConnectionId,
    string UserName,
    bool IsModerator
);
