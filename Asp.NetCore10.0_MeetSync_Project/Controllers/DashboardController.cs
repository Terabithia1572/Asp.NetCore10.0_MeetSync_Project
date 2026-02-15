using Microsoft.AspNetCore.Mvc;

namespace Asp.NetCore10._0_MeetSync_Project.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CreateRoom(string roomName)
        {
            return RedirectToAction("Index", "Meeting", new { roomName });
        }

        [HttpPost]
        public IActionResult JoinRoom(string roomName)
        {
            return RedirectToAction("Index", "Meeting", new { roomName });
        }
    }
}
