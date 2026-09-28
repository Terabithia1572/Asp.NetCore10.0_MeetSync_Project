using MeetSync.Application.DTOs.Ai;
using MeetSync.Application.Interfaces;
using MeetSync.Domain.Entities;
using MeetSync.Domain.Exceptions;
using MeetSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MeetSync.Infrastructure.Services;

public class TranscriptionService : ITranscriptionService
{
    private readonly MeetSyncDbContext _context;

    public TranscriptionService(MeetSyncDbContext context)
    {
        _context = context;
    }

    public async Task AddTranscriptChunkAsync(Guid roomId, string speakerName, string text, Guid? speakerUserId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var transcript = new MeetingTranscript
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            SpeakerUserId = speakerUserId,
            SpeakerName = string.IsNullOrWhiteSpace(speakerName) ? "Unknown" : speakerName,
            Text = text.Trim(),
            Timestamp = DateTime.UtcNow
        };

        _context.Transcripts.Add(transcript);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FullTranscriptResponseDto> GetRoomTranscriptAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var room = await _context.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);

        if (room == null) throw new NotFoundException("Room", roomId);

        var chunks = await _context.Transcripts
            .AsNoTracking()
            .Where(t => t.RoomId == roomId)
            .OrderBy(t => t.Timestamp)
            .Select(t => new TranscriptChunkDto(t.RoomId, t.SpeakerName, t.Text, t.Timestamp))
            .ToListAsync(cancellationToken);

        return new FullTranscriptResponseDto(room.Id, room.Name, chunks, chunks.Count);
    }
}
