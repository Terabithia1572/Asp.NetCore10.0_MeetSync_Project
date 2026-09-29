using MeetSync.Domain.Entities;
using MeetSync.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Asp.NetCore10._0_MeetSync_Project.Controllers
{
    public class MeetingController : Controller
    {
        private readonly IMeetingService _meetingService;
        private readonly IRoomService _roomService;

        public MeetingController(IMeetingService meetingService, IRoomService roomService)
        {
            _meetingService = meetingService;
            _roomService = roomService;
        }

        public async Task<IActionResult> Index(string roomName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(roomName)) return RedirectToAction("Index", "Dashboard");

            var decodedRoomName = RoomName.Clean(roomName);

            Guid? userId = Guid.TryParse(HttpContext.Session.GetString("userId"), out var id) ? id : null;
            var room = await _roomService.GetOrCreateRoomAsync(decodedRoomName, userId, cancellationToken);

            ViewBag.RoomId = room.Id;
            ViewBag.RoomName = room.Name;
            ViewBag.UserName = HttpContext.Session.GetString("username") ?? "Kullanıcı";
            return View();
        }
    }
}