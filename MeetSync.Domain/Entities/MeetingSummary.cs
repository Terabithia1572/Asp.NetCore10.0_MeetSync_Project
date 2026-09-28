namespace MeetSync.Domain.Entities;

public class MeetingSummary
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public string SummaryText { get; set; } = null!;

    public string KeyDecisionsJson { get; set; } = "[]";

    public string ActionItemsJson { get; set; } = "[]";

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public Room? Room { get; set; }
}
