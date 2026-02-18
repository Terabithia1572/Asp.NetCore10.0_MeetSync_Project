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
                // ✅ AJAX isteği mi kontrol et
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                    Request.Headers.Accept.Contains("application/json"))
                {
                    return Json(new { success = false, error = "Please enter a room name." });
                }

                ViewBag.Error = "Please enter a room name.";
                return View("Index");
            }

            // ✅ AJAX isteği mi kontrol et
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                Request.Headers.Accept.Contains("application/json"))
            {
                // AJAX için JSON döndür
                return Json(new { success = true, redirectUrl = $"/Meeting/Index?roomName={roomName}" });
            }

            // Normal POST ise redirect yap — Meeting sayfasına yönlendiriyoruz
            return RedirectToAction("Index", "Meeting", new { roomName = roomName });
        }
    }
}
