namespace MeetSync.Application.DTOs.Recording;

public record StartRecordingRequestDto(
    string RoomName,
    Guid? RecordedBy = null
);
