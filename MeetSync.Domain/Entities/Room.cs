namespace MeetSync.Domain.Entities;

public class Room
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public bool IsLobbyEnabled { get; set; } = false;

    public bool IsLocked { get; set; } = false;

    public string? RoomPassword { get; set; }

    public ICollection<RoomParticipant>? Participants { get; set; }

    public ICollection<Message>? Messages { get; set; }
}
