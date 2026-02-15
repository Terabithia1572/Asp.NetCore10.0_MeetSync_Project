using Microsoft.AspNetCore.Mvc;

namespace Asp.NetCore10._0_MeetSync_Project.Controllers
{
    public class AuthController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }
    }
}
