using Microsoft.AspNetCore.Mvc;

namespace Asp.NetCore10._0_MeetSync_Project.Controllers
{
    public class AuthController : Controller
    {
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            // Şimdilik basit kontrol, ileride Identity gelecek
            if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
            {
                // WebRTC odasında görünecek kullanıcı adı olarak email'in ilk kısmını alalım
                string username = email.Split('@')[0];
                HttpContext.Session.SetString("username", username);

                // ✅ AJAX isteği mi kontrol et
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                    Request.Headers.Accept.Contains("application/json"))
                {
                    // AJAX için JSON döndür
                    return Json(new { success = true, redirectUrl = "/Dashboard/Index" });
                }

                // Normal POST ise redirect yap
                return RedirectToAction("Index", "Dashboard");
            }

            // ✅ AJAX isteği mi kontrol et
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                Request.Headers.Accept.Contains("application/json"))
            {
                // AJAX için JSON hata döndür
                return Json(new { success = false, error = "Lütfen e-posta ve şifrenizi kontrol ediniz." });
            }

            ViewBag.Error = "Lütfen e-posta ve şifrenizi kontrol ediniz.";
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
