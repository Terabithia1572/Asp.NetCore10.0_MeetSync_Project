using System.Text.Json;
using MeetSync.Application.DTOs.Ai;
using MeetSync.Application.Interfaces;
using MeetSync.Domain.Entities;
using MeetSync.Domain.Exceptions;
using MeetSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MeetSync.Infrastructure.Services;

public class AiSummaryService : IAiSummaryService
{
    private readonly MeetSyncDbContext _context;
    private readonly ITranscriptionService _transcriptionService;

    public AiSummaryService(
        MeetSyncDbContext context,
        ITranscriptionService transcriptionService)
    {
        _context = context;
        _transcriptionService = transcriptionService;
    }

    public async Task<MeetingSummaryResponseDto> GenerateSummaryAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var room = await _context.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);

        if (room == null) throw new NotFoundException("Room", roomId);

        var transcriptData = await _transcriptionService.GetRoomTranscriptAsync(roomId, cancellationToken);
        var chunks = transcriptData.Chunks;

        string summaryText;
        List<string> keyDecisions = new();
        List<string> actionItems = new();

        if (chunks.Count == 0)
        {
            summaryText = $"Toplantı '{room.Name}' tamamlandı. Bu toplantı için henüz transkript kaydı oluşturulmadı.";
            keyDecisions.Add("Toplantı odası oluşturuldu ve canlı oturum tamamlandı.");
            actionItems.Add("Toplantı notlarının manuel olarak girilmesi önerilir.");
        }
        else
        {
            var fullText = string.Join(" ", chunks.Select(c => $"{c.SpeakerName}: {c.Text}"));
            var speakers = chunks.Select(c => c.SpeakerName).Distinct().ToList();

            summaryText = $"'{room.Name}' başlıklı toplantı {chunks.Count} transkript parçası ile başarıyla özetlendi. " +
                          $"Toplantıya katılan aktif konuşmacılar: {string.Join(", ", speakers)}.";

            foreach (var chunk in chunks)
            {
                var lower = chunk.Text.ToLowerInvariant();

                // Key Decisions Detection
                if (lower.Contains("karar") || lower.Contains("onay") || lower.Contains("kabul") || lower.Contains("kararlaştırdık") || lower.Contains("agree") || lower.Contains("decide"))
                {
                    keyDecisions.Add($"[{chunk.SpeakerName}]: {chunk.Text}");
                }

                // Action Items Detection
                if (lower.Contains("yapacak") || lower.Contains("görev") || lower.Contains("hallet") || lower.Contains("yapalım") || lower.Contains("todo") || lower.Contains("action") || lower.Contains("will do"))
                {
                    actionItems.Add($"[{chunk.SpeakerName}]: {chunk.Text}");
                }
            }

            if (keyDecisions.Count == 0)
            {
                keyDecisions.Add("Katılımcılar genel görüşme gerçekleştirdi, spesifik bir karar cümlesi tespit edilmedi.");
            }

            if (actionItems.Count == 0)
            {
                actionItems.Add("Takip edilecek yeni bir aksiyon maddesi tespit edilmedi.");
            }
        }

        var keyDecisionsJson = JsonSerializer.Serialize(keyDecisions);
        var actionItemsJson = JsonSerializer.Serialize(actionItems);

        // Check existing summary for room
        var existingSummary = await _context.Summaries.FirstOrDefaultAsync(s => s.RoomId == roomId, cancellationToken);
        if (existingSummary != null)
        {
            existingSummary.SummaryText = summaryText;
            existingSummary.KeyDecisionsJson = keyDecisionsJson;
            existingSummary.ActionItemsJson = actionItemsJson;
            existingSummary.GeneratedAt = DateTime.UtcNow;
        }
        else
        {
            var summary = new MeetingSummary
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                SummaryText = summaryText,
                KeyDecisionsJson = keyDecisionsJson,
                ActionItemsJson = actionItemsJson,
                GeneratedAt = DateTime.UtcNow
            };
            _context.Summaries.Add(summary);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new MeetingSummaryResponseDto(
            Guid.NewGuid(),
            room.Id,
            room.Name,
            summaryText,
            keyDecisions,
            actionItems,
            DateTime.UtcNow
        );
    }

    public async Task<MeetingSummaryResponseDto?> GetSummaryByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var summary = await _context.Summaries
            .AsNoTracking()
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.RoomId == roomId, cancellationToken);

        if (summary == null) return null;

        var keyDecisions = JsonSerializer.Deserialize<List<string>>(summary.KeyDecisionsJson) ?? new List<string>();
        var actionItems = JsonSerializer.Deserialize<List<string>>(summary.ActionItemsJson) ?? new List<string>();

        return new MeetingSummaryResponseDto(
            summary.Id,
            summary.RoomId,
            summary.Room?.Name ?? "Unknown",
            summary.SummaryText,
            keyDecisions,
            actionItems,
            summary.GeneratedAt
        );
    }
}
