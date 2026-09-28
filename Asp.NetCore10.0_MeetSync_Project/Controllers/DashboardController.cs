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

            var room = await _meetingService.PrepareMeetingRoomAsync(request, cancellationToken);

            if (isAjax)
            {
                return Json(new { success = true, redirectUrl = $"/Meeting/Index?roomName={Uri.EscapeDataString(room.Name)}" });
            }

            return RedirectToAction("Index", "Meeting", new { roomName = room.Name });
        }
    }
}
