using Microsoft.AspNetCore.Mvc;
using ElectionAdminPanel.Web.Models;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace ElectionAdminPanel.Web.Controllers
{
    public class AuthController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var client = _httpClientFactory.CreateClient();
                var loginData = new { email = model.Email, password = model.Password };
                var content = new StringContent(JsonConvert.SerializeObject(loginData), Encoding.UTF8, "application/json");

                var response = await client.PostAsync("http://localhost:5110/api/auth/admin/login", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    dynamic? result = JsonConvert.DeserializeObject(responseContent);

                    if (result != null && result.success == true)
                    {
                        var token = (string)result.data.token;
                        var userEmail = (string)result.data.user.email;
                        var userRole = (string)result.data.user.role;

                        if (token != null && userEmail != null && userRole != null)
                        {
                            // Store token in session instead of cookie to avoid HTTP 431 errors
                            HttpContext.Session.SetString("AccessToken", token);
                            
                            var claims = new List<Claim>
                            {
                                new Claim(ClaimTypes.Name, userEmail),
                                new Claim(ClaimTypes.Role, userRole)
                                // Removed AccessToken from claims to reduce cookie size
                            };

                            var claimsIdentity = new ClaimsIdentity(
                                claims, CookieAuthenticationDefaults.AuthenticationScheme);

                            var authProperties = new AuthenticationProperties
                            {
                                IsPersistent = true, // Remember me
                                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) // Token expiration
                            };

                            await HttpContext.SignInAsync(
                                CookieAuthenticationDefaults.AuthenticationScheme,
                                new ClaimsPrincipal(claimsIdentity),
                                authProperties);

                            return RedirectToAction("Index", "Home");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, result?.message ?? "An unknown error occurred.");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ModelState.AddModelError(string.Empty, $"API Error: {response.StatusCode} - {errorContent}");
                }
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Auth");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}