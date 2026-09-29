using MeetSync.Application.DTOs.Recording;
using MeetSync.Application.Interfaces;
using MeetSync.Domain.Entities;
using MeetSync.Domain.Enums;
using MeetSync.Domain.Exceptions;
using MeetSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace MeetSync.Infrastructure.Services;

public class MeetingRecordingService : IMeetingRecordingService
{
    private readonly MeetSyncDbContext _context;
    private readonly IRoomService _roomService;
    private readonly string _recordingsFolder;

    public MeetingRecordingService(
        MeetSyncDbContext context,
        IRoomService roomService,
        IHostEnvironment environment)
    {
        _context = context;
        _roomService = roomService;

        _recordingsFolder = Path.Combine(environment.ContentRootPath, "wwwroot", "recordings");

        if (!Directory.Exists(_recordingsFolder))
        {
            Directory.CreateDirectory(_recordingsFolder);
        }
    }

    public async Task<RecordingResponseDto> StartRecordingAsync(StartRecordingRequestDto request, CancellationToken cancellationToken = default)
    {
        var room = await _roomService.GetRoomByNameAsync(request.RoomName, cancellationToken);
        if (room == null)
        {
            throw new NotFoundException("Room", request.RoomName);
        }

        var recordingId = Guid.NewGuid();
        var fileName = $"recording_{room.Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.webm";
        var filePath = Path.Combine(_recordingsFolder, $"{recordingId}.webm");

        var recording = new MeetingRecording
        {
            Id = recordingId,
            RoomId = room.Id,
            FileName = fileName,
            FilePath = filePath,
            FileSize = 0,
            DurationSeconds = 0,
            RecordedBy = request.RecordedBy,
            StartedAt = DateTime.UtcNow,
            Status = RecordingStatus.Processing
        };

        _context.Recordings.Add(recording);
        await _context.SaveChangesAsync(cancellationToken);

        return new RecordingResponseDto(
            recording.Id,
            recording.RoomId,
            room.Name,
            recording.FileName,
            $"/recordings/{recordingId}.webm",
            recording.FileSize,
            recording.DurationSeconds,
            recording.StartedAt,
            recording.EndedAt,
            recording.Status
        );
    }

    public async Task<RecordingResponseDto> StopRecordingAsync(StopRecordingRequestDto request, CancellationToken cancellationToken = default)
    {
        var recording = await _context.Recordings
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.Id == request.RecordingId, cancellationToken);

        if (recording == null)
        {
            throw new NotFoundException("MeetingRecording", request.RecordingId);
        }

        recording.EndedAt = DateTime.UtcNow;
        recording.DurationSeconds = Math.Max(0, request.DurationSeconds);

        if (File.Exists(recording.FilePath))
        {
            var fileInfo = new FileInfo(recording.FilePath);
            recording.FileSize = fileInfo.Length;
            recording.Status = !request.Failed && fileInfo.Length > 0 ? RecordingStatus.Completed : RecordingStatus.Failed;
        }
        else
        {
            recording.Status = RecordingStatus.Failed;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var roomName = recording.Room?.Name ?? "Unknown";
        return new RecordingResponseDto(
            recording.Id,
            recording.RoomId,
            roomName,
            recording.FileName,
            $"/recordings/{recording.Id}.webm",
            recording.FileSize,
            recording.DurationSeconds,
            recording.StartedAt,
            recording.EndedAt,
            recording.Status
        );
    }

    public async Task AppendChunkAsync(Guid recordingId, byte[] chunkData, CancellationToken cancellationToken = default)
    {
        if (chunkData == null || chunkData.Length == 0) return;

        var recording = await _context.Recordings.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == recordingId, cancellationToken);
        if (recording == null || recording.Status != RecordingStatus.Processing)
            throw new InvalidOperationException("Recording is not accepting data.");
        if (chunkData.Length > 12 * 1024)
            throw new InvalidOperationException("Recording chunk is too large.");
        var filePath = recording.FilePath;
        await using var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await fileStream.WriteAsync(chunkData, cancellationToken);
    }

    public async Task<RecordingResponseDto?> GetRecordingByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var recording = await _context.Recordings
            .AsNoTracking()
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (recording == null) throw new NotFoundException("MeetingRecording", id);

        var roomName = recording.Room?.Name ?? "Unknown";
        return new RecordingResponseDto(
            recording.Id,
            recording.RoomId,
            roomName,
            recording.FileName,
            $"/recordings/{recording.Id}.webm",
            recording.FileSize,
            recording.DurationSeconds,
            recording.StartedAt,
            recording.EndedAt,
            recording.Status
        );
    }

    public async Task<IReadOnlyList<RecordingResponseDto>> GetRecordingsByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var recordings = await _context.Recordings
            .AsNoTracking()
            .Include(r => r.Room)
            .Where(r => r.RoomId == roomId)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(cancellationToken);

        return recordings.Select(r => new RecordingResponseDto(
            r.Id,
            r.RoomId,
            r.Room?.Name ?? "Unknown",
            r.FileName,
            $"/recordings/{r.Id}.webm",
            r.FileSize,
            r.DurationSeconds,
            r.StartedAt,
            r.EndedAt,
            r.Status
        )).ToList();
    }

    public async Task<Stream?> GetRecordingFileStreamAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var recording = await _context.Recordings
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (recording == null || !File.Exists(recording.FilePath))
        {
            return null;
        }

        return new FileStream(recording.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
    }

    public async Task<bool> DeleteRecordingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var recording = await _context.Recordings.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (recording == null) return false;

        if (File.Exists(recording.FilePath))
        {
            try { File.Delete(recording.FilePath); } catch { }
        }

        _context.Recordings.Remove(recording);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
