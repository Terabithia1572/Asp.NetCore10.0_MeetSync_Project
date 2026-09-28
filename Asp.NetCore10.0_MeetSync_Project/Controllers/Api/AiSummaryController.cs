using MeetSync.Application.DTOs.Ai;
using MeetSync.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Asp.NetCore10._0_MeetSync_Project.Controllers.Api;

/// <summary>
/// RESTful API endpoints for AI live captions and meeting summaries.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AiSummaryController : ControllerBase
{
    private readonly IAiSummaryService _aiSummaryService;
    private readonly ITranscriptionService _transcriptionService;

    public AiSummaryController(
        IAiSummaryService aiSummaryService,
        ITranscriptionService transcriptionService)
    {
        _aiSummaryService = aiSummaryService;
        _transcriptionService = transcriptionService;
    }

    /// <summary>
    /// Gets generated AI summary for a specific room.
    /// </summary>
    /// <param name="roomId">Guid of the room.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Meeting summary response dto.</returns>
    [HttpGet("room/{roomId:guid}")]
    [ProducesResponseType(typeof(MeetingSummaryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummary([FromRoute] Guid roomId, CancellationToken cancellationToken)
    {
        var summary = await _aiSummaryService.GetSummaryByRoomIdAsync(roomId, cancellationToken);
        if (summary == null) return NotFound();
        return Ok(summary);
    }

    /// <summary>
    /// Gets full transcript for a room.
    /// </summary>
    /// <param name="roomId">Guid of the room.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full transcript response dto.</returns>
    [HttpGet("room/{roomId:guid}/transcript")]
    [ProducesResponseType(typeof(FullTranscriptResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTranscript([FromRoute] Guid roomId, CancellationToken cancellationToken)
    {
        var transcript = await _transcriptionService.GetRoomTranscriptAsync(roomId, cancellationToken);
        return Ok(transcript);
    }

    /// <summary>
    /// Manually triggers AI summary generation for a room.
    /// </summary>
    /// <param name="roomId">Guid of the room.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated meeting summary.</returns>
    [HttpPost("room/{roomId:guid}/generate")]
    [ProducesResponseType(typeof(MeetingSummaryResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateSummary([FromRoute] Guid roomId, CancellationToken cancellationToken)
    {
        var summary = await _aiSummaryService.GenerateSummaryAsync(roomId, cancellationToken);
        return Ok(summary);
    }
}
