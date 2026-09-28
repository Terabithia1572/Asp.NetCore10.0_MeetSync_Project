namespace MeetSync.Application.DTOs.Ai;

public record MeetingSummaryResponseDto(
    Guid Id,
    Guid RoomId,
    string RoomName,
    string SummaryText,
    IReadOnlyList<string> KeyDecisions,
    IReadOnlyList<string> ActionItems,
    DateTime GeneratedAt
);
