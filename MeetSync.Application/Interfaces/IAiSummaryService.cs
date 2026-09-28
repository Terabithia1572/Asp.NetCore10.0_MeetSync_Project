using MeetSync.Application.DTOs.Ai;

namespace MeetSync.Application.Interfaces;

public interface IAiSummaryService
{
    Task<MeetingSummaryResponseDto> GenerateSummaryAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<MeetingSummaryResponseDto?> GetSummaryByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default);
}
