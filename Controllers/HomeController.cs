using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ElectionAdminPanel.Web.Controllers
{
    [Authorize] // Requires authentication for all actions in this controller
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Bem-vindo!";
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Authorize(Roles = "admin")]
        public IActionResult TestSealedRestrictions()
        {
            return View();
        }
    }
}