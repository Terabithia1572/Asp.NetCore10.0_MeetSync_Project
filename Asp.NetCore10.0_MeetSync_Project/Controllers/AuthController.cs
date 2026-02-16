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

                return RedirectToAction("Index", "Dashboard");
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