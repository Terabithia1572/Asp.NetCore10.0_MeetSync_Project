using MeetSync.Application.DTOs.Ai;

namespace MeetSync.Application.Interfaces;

public interface ITranscriptionService
{
    Task AddTranscriptChunkAsync(Guid roomId, string speakerName, string text, Guid? speakerUserId = null, CancellationToken cancellationToken = default);
    Task<FullTranscriptResponseDto> GetRoomTranscriptAsync(Guid roomId, CancellationToken cancellationToken = default);
}
