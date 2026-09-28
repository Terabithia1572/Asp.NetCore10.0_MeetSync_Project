using MeetSync.Application.DTOs.Recording;

namespace MeetSync.Application.Interfaces;

public interface IMeetingRecordingService
{
    Task<RecordingResponseDto> StartRecordingAsync(StartRecordingRequestDto request, CancellationToken cancellationToken = default);
    Task<RecordingResponseDto> StopRecordingAsync(StopRecordingRequestDto request, CancellationToken cancellationToken = default);
    Task AppendChunkAsync(Guid recordingId, byte[] chunkData, CancellationToken cancellationToken = default);
    Task<RecordingResponseDto?> GetRecordingByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecordingResponseDto>> GetRecordingsByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<Stream?> GetRecordingFileStreamAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> DeleteRecordingAsync(Guid id, CancellationToken cancellationToken = default);
}
