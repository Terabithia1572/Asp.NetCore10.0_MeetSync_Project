using MeetSync.Domain.Entities;
using MeetSync.Application.DTOs.Room;
using MeetSync.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Asp.NetCore10._0_MeetSync_Project.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IMeetingService _meetingService;

        public DashboardController(IMeetingService meetingService)
        {
            _meetingService = meetingService;
        }

        public IActionResult Index()
        {
            ViewBag.UserName = HttpContext.Session.GetString("username") ?? "User";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> JoinRoom([FromForm] JoinRoomRequestDto request, CancellationToken cancellationToken)
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                         Request.Headers.Accept.ToString().Contains("application/json");

            if (string.IsNullOrWhiteSpace(request.RoomName))
            {
                if (isAjax)
                {
                    return Json(new { success = false, error = "Please enter a room name." });
                }

                ViewBag.Error = "Please enter a room name.";
                return View("Index");
            }

            var decodedRoomName = RoomName.Clean(request.RoomName);
            Guid? userId = Guid.TryParse(HttpContext.Session.GetString("userId"), out var id) ? id : null;
            var cleanRequest = request with { RoomName = decodedRoomName, CreatedBy = userId };

            var room = await _meetingService.PrepareMeetingRoomAsync(cleanRequest, cancellationToken);

            ViewBag.RoomId = room.Id;
            ViewBag.RoomName = room.Name;

            if (isAjax)
            {
                return Json(new { success = true, redirectUrl = $"/Meeting/Index?roomName={Uri.EscapeDataString(room.Name)}", roomId = room.Id });
            }

            return RedirectToAction("Index", "Meeting", new { roomName = room.Name });
        }
    }
}
