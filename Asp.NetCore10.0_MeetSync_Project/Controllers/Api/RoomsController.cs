using MeetSync.Application.DTOs.Room;
using MeetSync.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Asp.NetCore10._0_MeetSync_Project.Controllers.Api;

/// <summary>
/// RESTful API endpoints for meeting room management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService;
    }

    /// <summary>
    /// Creates a new meeting room.
    /// </summary>
    /// <param name="request">The room creation request model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created room details.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(RoomResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateRoomRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _roomService.CreateRoomAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Gets room details by unique room identifier.
    /// </summary>
    /// <param name="id">Guid identifier of the room.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Room details if found.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RoomResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var room = await _roomService.GetRoomByIdAsync(id, cancellationToken);
        if (room == null) return NotFound();
        return Ok(room);
    }

    /// <summary>
    /// Gets room details by room name.
    /// </summary>
    /// <param name="name">Name of the room.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Room details if found.</returns>
    [HttpGet("by-name/{name}")]
    [ProducesResponseType(typeof(RoomResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByName([FromRoute] string name, CancellationToken cancellationToken)
    {
        var room = await _roomService.GetRoomByNameAsync(name, cancellationToken);
        if (room == null) return NotFound();
        return Ok(room);
    }
}
