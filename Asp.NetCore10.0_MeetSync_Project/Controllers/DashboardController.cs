using Microsoft.AspNetCore.Mvc;

namespace Asp.NetCore10._0_MeetSync_Project.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            // Session'dan kullanıcı ismini alıp View'a yolluyoruz (Welcome Alex kısmı için)
            ViewBag.UserName = HttpContext.Session.GetString("username") ?? "User";
            return View();
        }

        [HttpPost]
        public IActionResult JoinRoom(string roomName)
        {
            if (string.IsNullOrEmpty(roomName))
            {
                ViewBag.Error = "Please enter a room name.";
                return View("Index");
            }
            // Meeting sayfasına yönlendiriyoruz
            return RedirectToAction("Index", "Meeting", new { roomName = roomName });
        }
    }
}