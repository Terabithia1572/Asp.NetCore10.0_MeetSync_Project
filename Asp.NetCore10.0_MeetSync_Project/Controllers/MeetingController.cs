using Microsoft.AspNetCore.Mvc;

namespace Asp.NetCore10._0_MeetSync_Project.Controllers
{
    public class MeetingController : Controller
    {
        public IActionResult Index(string roomName)
        {
            if (string.IsNullOrEmpty(roomName)) return RedirectToAction("Index", "Dashboard");

            ViewBag.RoomName = roomName;
            ViewBag.UserName = HttpContext.Session.GetString("username") ?? "Kullanıcı";
            return View();
        }
    }
}