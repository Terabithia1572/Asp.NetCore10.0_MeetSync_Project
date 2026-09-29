namespace MeetSync.Application.DTOs.Recording;

public record StopRecordingRequestDto(
    Guid RecordingId,
    int DurationSeconds,
    bool Failed = false
);
