using MeetSync.Application.DTOs.Room;
using MeetSync.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Asp.NetCore10._0_MeetSync_Project.Controllers
{
    public class RoomController : Controller
    {
        private readonly IRoomService _roomService;

        public RoomController(IRoomService roomService)
        {
            _roomService = roomService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] CreateRoomRequestDto request, CancellationToken cancellationToken)
        {
            var userIdString = HttpContext.Session.GetString("userId");
            Guid? hostUserId = Guid.TryParse(userIdString, out var parsedGuid) ? parsedGuid : null;

            var createDto = request with { CreatedBy = hostUserId };
            var room = await _roomService.CreateRoomAsync(createDto, cancellationToken);

            return RedirectToAction("Index", new { id = room.Id });
        }

        [Route("room/{id:guid}")]
        public async Task<IActionResult> Index(Guid id, CancellationToken cancellationToken)
        {
            var room = await _roomService.GetRoomByIdAsync(id, cancellationToken);
            if (room == null) return NotFound();

            return RedirectToAction("Index", "Meeting", new { roomName = room.Name });
        }
    }
}
