using MeetSync.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Asp.NetCore10._0_MeetSync_Project.Controllers
{
    public class MeetingController : Controller
    {
        private readonly IMeetingService _meetingService;

        public MeetingController(IMeetingService meetingService)
        {
            _meetingService = meetingService;
        }

        public async Task<IActionResult> Index(string roomName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(roomName)) return RedirectToAction("Index", "Dashboard");

            var canJoin = await _meetingService.CanJoinMeetingAsync(roomName, cancellationToken);
            if (!canJoin) return RedirectToAction("Index", "Dashboard");

            ViewBag.RoomName = roomName;
            ViewBag.UserName = HttpContext.Session.GetString("username") ?? "Kullanıcı";
            return View();
        }
    }
}