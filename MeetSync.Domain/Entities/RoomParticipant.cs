namespace MeetSync.Domain.Entities;

public class RoomParticipant
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public Guid UserId { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LeftAt { get; set; }

    public Room? Room { get; set; }

    public AppUser? User { get; set; }
}
