namespace MeetSync.Application.DTOs.Ai;

public record TranscriptChunkDto(
    Guid RoomId,
    string SpeakerName,
    string Text,
    DateTime Timestamp
);
