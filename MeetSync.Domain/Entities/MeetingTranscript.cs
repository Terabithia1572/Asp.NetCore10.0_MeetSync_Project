namespace MeetSync.Domain.Entities;

public class MeetingTranscript
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public Guid? SpeakerUserId { get; set; }

    public string SpeakerName { get; set; } = null!;

    public string Text { get; set; } = null!;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Room? Room { get; set; }
}
