namespace MeetSync.Domain.Entities;

public class Message
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public Guid UserId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public Room? Room { get; set; }

    public AppUser? User { get; set; }
}
