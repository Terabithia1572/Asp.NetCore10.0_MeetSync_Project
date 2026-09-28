using MeetSync.Application.DTOs.Auth;
using MeetSync.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Asp.NetCore10._0_MeetSync_Project.Controllers
{
    [EnableRateLimiting("AuthPolicy")]
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromForm] LoginRequestDto request, CancellationToken cancellationToken)
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                         Request.Headers.Accept.ToString().Contains("application/json");

            var result = await _authService.LoginAsync(request, cancellationToken);

            if (result.Success && result.User != null)
            {
                HttpContext.Session.SetString("username", result.User.DisplayName);
                HttpContext.Session.SetString("userId", result.User.Id.ToString());

                if (isAjax)
                {
                    return Json(new { success = true, redirectUrl = "/Dashboard/Index" });
                }

                return RedirectToAction("Index", "Dashboard");
            }

            if (isAjax)
            {
                return Json(new { success = false, error = result.Message ?? "Invalid email or password." });
            }

            ViewBag.Error = result.Message ?? "Invalid email or password.";
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register([FromForm] RegisterRequestDto request, CancellationToken cancellationToken)
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                         Request.Headers.Accept.ToString().Contains("application/json");

            var result = await _authService.RegisterAsync(request, cancellationToken);

            if (result.Success && result.User != null)
            {
                HttpContext.Session.SetString("username", result.User.DisplayName);
                HttpContext.Session.SetString("userId", result.User.Id.ToString());

                if (isAjax)
                {
                    return Json(new { success = true, redirectUrl = "/Dashboard/Index" });
                }

                return RedirectToAction("Index", "Dashboard");
            }

            if (isAjax)
            {
                return Json(new { success = false, error = result.Message ?? "Registration failed." });
            }

            ViewBag.Error = result.Message ?? "Registration failed.";
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
