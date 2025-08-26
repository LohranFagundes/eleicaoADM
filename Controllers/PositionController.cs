using Microsoft.AspNetCore.Mvc;
using ElectionAdminPanel.Web.Models;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using ElectionAdminPanel.Web.Filters;

namespace ElectionAdminPanel.Web.Controllers
{
    [Authorize(Roles = "admin")] // Only admin can access position management
    public class PositionController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiBaseUrl = "http://localhost:5110/api/position";
        private readonly string _electionApiBaseUrl = "http://localhost:5110/api/election";

        public PositionController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        private HttpClient CreateAuthorizedClient()
        {
            var client = _httpClientFactory.CreateClient();
            var token = HttpContext.Session.GetString("AccessToken");
            System.Console.WriteLine($"[DEBUG] PositionController - Token available: {!string.IsNullOrEmpty(token)}");
            System.Console.WriteLine($"[DEBUG] PositionController - User authenticated: {User.Identity?.IsAuthenticated}");
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                System.Console.WriteLine($"[DEBUG] PositionController - Token set in header");
            }
            return client;
        }

        public async Task<IActionResult> List(string search = "", int? electionId = null, int page = 1, int limit = 10)
        {
            var client = CreateAuthorizedClient();
            var url = $"{_apiBaseUrl}?page={page}&limit={limit}";

            if (!string.IsNullOrEmpty(search))
            {
                url += $"&search={search}";
            }
            if (electionId.HasValue)
            {
                url += $"&electionId={electionId.Value}";
            }

            System.Console.WriteLine($"[DEBUG] Position List - URL: {url}");
            var response = await client.GetAsync(url);

            System.Console.WriteLine($"[DEBUG] Position List - Response Status: {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                System.Console.WriteLine($"[DEBUG] Position List - Response Content: {responseContent}");
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<PositionListResponse>>(responseContent);
                System.Console.WriteLine($"[DEBUG] Position List - API Success: {apiResponse?.Success}, Items Count: {apiResponse?.Data?.Items?.Count ?? 0}");
                if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                {
                    var viewModel = new PositionListViewModel
                    {
                        Positions = apiResponse.Data.Items,
                        Search = search,
                        ElectionId = electionId,
                        CurrentPage = apiResponse.Data.CurrentPage,
                        TotalPages = apiResponse.Data.TotalPages,
                        HasNextPage = apiResponse.Data.HasNextPage,
                        HasPreviousPage = apiResponse.Data.HasPreviousPage
                    };

                    await PopulateElectionsDropdown(electionId);

                    return View(viewModel);
                }
                ModelState.AddModelError(string.Empty, apiResponse?.Message ?? "An unknown error occurred.");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                TempData["ErrorMessage"] = "Erro de autenticação. Faça login novamente.";
                return RedirectToAction("Login", "Auth");
            }
            else
            {
                ModelState.AddModelError(string.Empty, $"API Error: {response.StatusCode}");
            }
            return View(new PositionListViewModel { Positions = new List<PositionModel>() });
        }

        [HttpGet]
        [RestrictWhenSealed]
        public async Task<IActionResult> Create()
        {
            await PopulateElectionsDropdown();
            return View();
        }

        [HttpPost]
        [RestrictWhenSealed]
        public async Task<IActionResult> Create(PositionModel model)
        {
            if (ModelState.IsValid)
            {
                var client = CreateAuthorizedClient();
                var content = new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(_apiBaseUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<PositionModel>>(responseContent);
                    if (apiResponse != null && apiResponse.Success)
                    {
                        TempData["SuccessMessage"] = "Cargo criado com sucesso!";
                        return RedirectToAction("List");
                    }
                    ModelState.AddModelError(string.Empty, apiResponse?.Message ?? "An unknown error occurred.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ModelState.AddModelError(string.Empty, $"API Error: {response.StatusCode} - {errorContent}");
                }
            }
            await PopulateElectionsDropdown();
            return View(model);
        }

        [HttpGet]
        [RestrictWhenSealed]
        public async Task<IActionResult> Edit(int id)
        {
            var client = CreateAuthorizedClient();
            var response = await client.GetAsync($"{_apiBaseUrl}/{id}");

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<PositionModel>>(responseContent);
                if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                {
                    await PopulateElectionsDropdown(apiResponse.Data.ElectionId);
                    return View(apiResponse.Data);
                }
                ModelState.AddModelError(string.Empty, apiResponse?.Message ?? "An unknown error occurred.");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return NotFound();
            }
            else
            {
                ModelState.AddModelError(string.Empty, $"API Error: {response.StatusCode}");
            }
            return RedirectToAction("List");
        }

        [HttpPost]
        [RestrictWhenSealed]
        public async Task<IActionResult> Edit(int id, PositionModel model)
        {
            if (ModelState.IsValid)
            {
                var client = CreateAuthorizedClient();
                var content = new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json");
                var response = await client.PutAsync($"{_apiBaseUrl}/{id}", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<PositionModel>>(responseContent);
                    if (apiResponse != null && apiResponse.Success)
                    {
                        TempData["SuccessMessage"] = "Cargo atualizado com sucesso!";
                        return RedirectToAction("List");
                    }
                    ModelState.AddModelError(string.Empty, apiResponse?.Message ?? "An unknown error occurred.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ModelState.AddModelError(string.Empty, $"API Error: {response.StatusCode} - {errorContent}");
                }
            }
            await PopulateElectionsDropdown(model.ElectionId);
            return View(model);
        }

        [HttpPost]
        [RestrictWhenSealed]
        public async Task<IActionResult> Delete(int id)
        {
            var client = CreateAuthorizedClient();
            var response = await client.DeleteAsync($"{_apiBaseUrl}/{id}");

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<object>>(responseContent);
                if (apiResponse != null && apiResponse.Success)
                {
                    TempData["SuccessMessage"] = "Cargo excluído com sucesso!";
                }
                else
                {
                    TempData["ErrorMessage"] = apiResponse?.Message ?? "An unknown error occurred.";
                }
            }
            else
            {
                TempData["ErrorMessage"] = $"API Error: {response.StatusCode}";
            }
            return RedirectToAction("List");
        }

        private async Task PopulateElectionsDropdown(int? selectedElectionId = null)
        {
            var client = CreateAuthorizedClient();
            var electionsResponse = await client.GetAsync($"{_electionApiBaseUrl}");
            if (electionsResponse.IsSuccessStatusCode)
            {
                var electionsContent = await electionsResponse.Content.ReadAsStringAsync();
                var electionsApiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<ElectionListResponse>>(electionsContent);
                if (electionsApiResponse != null && electionsApiResponse.Success && electionsApiResponse.Data != null)
                {
                    ViewBag.Elections = new SelectList(electionsApiResponse.Data.Items, "Id", "Title", selectedElectionId);
                }
            }
        }
    }
}
