using MeetSync.Application.DTOs.Recording;
using MeetSync.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Asp.NetCore10._0_MeetSync_Project.Controllers.Api;

/// <summary>
/// RESTful API endpoints for meeting recordings.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RecordingsController : ControllerBase
{
    private readonly IMeetingRecordingService _recordingService;

    public RecordingsController(IMeetingRecordingService recordingService)
    {
        _recordingService = recordingService;
    }

    /// <summary>
    /// Gets all recordings for a specific room.
    /// </summary>
    /// <param name="roomId">Guid of the room.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recording metadata.</returns>
    [HttpGet("room/{roomId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<RecordingResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByRoomId([FromRoute] Guid roomId, CancellationToken cancellationToken)
    {
        var recordings = await _recordingService.GetRecordingsByRoomIdAsync(roomId, cancellationToken);
        return Ok(recordings);
    }

    /// <summary>
    /// Gets recording details by ID.
    /// </summary>
    /// <param name="id">Guid of the recording.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recording metadata.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RecordingResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var recording = await _recordingService.GetRecordingByIdAsync(id, cancellationToken);
        if (recording == null) return NotFound();
        return Ok(recording);
    }

    /// <summary>
    /// Downloads the recorded video file stream.
    /// </summary>
    /// <param name="id">Guid of the recording.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Video file stream.</returns>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var recording = await _recordingService.GetRecordingByIdAsync(id, cancellationToken);
        if (recording == null) return NotFound();

        var stream = await _recordingService.GetRecordingFileStreamAsync(id, cancellationToken);
        if (stream == null) return NotFound();

        return File(stream, "video/webm", recording.FileName);
    }

    /// <summary>
    /// Deletes a recording by ID.
    /// </summary>
    /// <param name="id">Guid of the recording.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var success = await _recordingService.DeleteRecordingAsync(id, cancellationToken);
        if (!success) return NotFound();
        return NoContent();
    }
}
