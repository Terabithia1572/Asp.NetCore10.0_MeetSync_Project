using MeetSync.Domain.Enums;

namespace MeetSync.Domain.Entities;

public class MeetingRecording
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public string FileName { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public long FileSize { get; set; }

    public int DurationSeconds { get; set; }

    public Guid? RecordedBy { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; set; }

    public RecordingStatus Status { get; set; } = RecordingStatus.Processing;

    public Room? Room { get; set; }
}
