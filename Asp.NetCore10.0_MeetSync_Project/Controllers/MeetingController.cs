using Microsoft.AspNetCore.Mvc;

namespace Asp.NetCore10._0_MeetSync_Project.Controllers
{
    public class MeetingController : Controller
    {
        public IActionResult Index(string roomName)
        {
            ViewBag.RoomName = roomName;
            return View();
        }
    }
}
