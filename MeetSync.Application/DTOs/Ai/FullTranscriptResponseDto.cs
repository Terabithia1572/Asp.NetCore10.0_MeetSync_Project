namespace MeetSync.Application.DTOs.Ai;

public record FullTranscriptResponseDto(
    Guid RoomId,
    string RoomName,
    IReadOnlyList<TranscriptChunkDto> Chunks,
    int TotalChunks
);
