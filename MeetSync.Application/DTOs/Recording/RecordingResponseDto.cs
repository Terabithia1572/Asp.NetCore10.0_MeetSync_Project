using MeetSync.Domain.Enums;

namespace MeetSync.Application.DTOs.Recording;

public record RecordingResponseDto(
    Guid Id,
    Guid RoomId,
    string RoomName,
    string FileName,
    string FilePath,
    long FileSize,
    int DurationSeconds,
    DateTime StartedAt,
    DateTime? EndedAt,
    RecordingStatus Status
);
