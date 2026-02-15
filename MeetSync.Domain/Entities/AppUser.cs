namespace MeetSync.Domain.Entities;

public class AppUser
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RoomParticipant>? Participants { get; set; }
}
