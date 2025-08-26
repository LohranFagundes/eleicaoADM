using Microsoft.AspNetCore.Mvc;
using ElectionAdminPanel.Web.Models;
using System.Net.Http;
using System.Text; 
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ElectionAdminPanel.Web.Filters;
using ElectionAdminPanel.Web.Services;

namespace ElectionAdminPanel.Web.Controllers
{
    [Authorize(Roles = "admin")] // Only admin can access election management
    public class ElectionController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISealedElectionService _sealedElectionService;
        private readonly string _apiBaseUrl = "http://localhost:5110/api/election";

        public ElectionController(IHttpClientFactory httpClientFactory, ISealedElectionService sealedElectionService)
        {
            _httpClientFactory = httpClientFactory;
            _sealedElectionService = sealedElectionService;
        }

        private HttpClient CreateAuthorizedClient()
        {
            var client = _httpClientFactory.CreateClient();
            var token = HttpContext.Session.GetString("AccessToken");
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            return client;
        }

        [HttpGet]
        public async Task<IActionResult> List(int page = 1, int limit = 10, string status = "", string type = "")
        {
            var client = CreateAuthorizedClient();
            var queryParams = new List<string>();
            
            queryParams.Add($"page={page}");
            queryParams.Add($"limit={limit}");
            
            if (!string.IsNullOrEmpty(status))
                queryParams.Add($"status={status}");
                
            if (!string.IsNullOrEmpty(type))
                queryParams.Add($"type={type}");
            
            var queryString = string.Join("&", queryParams);
            var url = $"{_apiBaseUrl}?{queryString}";
            
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.ElectionListResponse>>(responseContent);
                if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                {
                    ViewBag.CurrentPage = page;
                    ViewBag.PageLimit = limit;
                    ViewBag.StatusFilter = status;
                    ViewBag.TypeFilter = type;
                    ViewBag.TotalPages = apiResponse.Data.TotalPages;
                    ViewBag.TotalItems = apiResponse.Data.TotalItems;
                    ViewBag.HasNextPage = apiResponse.Data.HasNextPage;
                    ViewBag.HasPreviousPage = apiResponse.Data.HasPreviousPage;
                    
                    return View(apiResponse.Data);
                }
                ModelState.AddModelError(string.Empty, apiResponse?.Message ?? "An unknown error occurred.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, $"API Error: {response.StatusCode}");
            }
            return View(new Models.ElectionListResponse { Items = new List<Models.ElectionModel>() });
        }

        [HttpGet]
        [RestrictWhenSealed]
        public async Task<IActionResult> Create()
        {
            await LoadCompanies();
            return View();
        }

        [HttpPost]
        [RestrictWhenSealed]
        public async Task<IActionResult> Create(ElectionModel model)
        {
            if (ModelState.IsValid)
            {
                var client = CreateAuthorizedClient();
                var requestData = new
                {
                    title = model.Title,
                    description = model.Description,
                    electionType = model.ElectionType,
                    startDate = model.StartDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    endDate = model.EndDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    timezone = model.Timezone,
                    allowBlankVotes = model.AllowBlankVotes,
                    allowNullVotes = model.AllowNullVotes,
                    requireJustification = model.RequireJustification,
                    maxVotesPerVoter = model.MaxVotesPerVoter,
                    votingMethod = model.VotingMethod,
                    resultsVisibility = model.ResultsVisibility,
                    companyId = model.CompanyId
                };

                // Debug log
                System.Console.WriteLine($"[DEBUG] Creating election with data: {JsonConvert.SerializeObject(requestData)}");
                System.Console.WriteLine($"[DEBUG] API URL: {_apiBaseUrl}");
                System.Console.WriteLine($"[DEBUG] Token present: {client.DefaultRequestHeaders.Authorization != null}");

                var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(_apiBaseUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"[DEBUG] Success Response: {responseContent}");
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.ElectionModel>>(responseContent);
                    if (apiResponse != null && apiResponse.Success)
                    {
                        TempData["SuccessMessage"] = "Eleição criada com sucesso!";
                        return RedirectToAction("List");
                    }
                    ModelState.AddModelError(string.Empty, apiResponse?.Message ?? "An unknown error occurred.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"[DEBUG] Create Election API Error: Status={response.StatusCode}, Content={errorContent}");
                    ModelState.AddModelError(string.Empty, $"API Error: {response.StatusCode} - {errorContent}");
                }
            }
            await LoadCompanies();
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
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.ElectionModel>>(responseContent);
                if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                {
                    if (Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(apiResponse.Data);
                    }
                    await LoadCompanies();
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
        public async Task<IActionResult> Edit(int id, ElectionModel model)
        {
            if (ModelState.IsValid)
            {
                var client = CreateAuthorizedClient();
                var requestData = new
                {
                    title = model.Title,
                    description = model.Description,
                    electionType = model.ElectionType,
                    startDate = model.StartDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    endDate = model.EndDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    timezone = model.Timezone,
                    allowBlankVotes = model.AllowBlankVotes,
                    allowNullVotes = model.AllowNullVotes,
                    requireJustification = model.RequireJustification,
                    maxVotesPerVoter = model.MaxVotesPerVoter,
                    votingMethod = model.VotingMethod,
                    resultsVisibility = model.ResultsVisibility,
                    companyId = model.CompanyId
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
                var response = await client.PutAsync($"{_apiBaseUrl}/{id}", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.ElectionModel>>(responseContent);
                    if (apiResponse != null && apiResponse.Success)
                    {
                        TempData["SuccessMessage"] = "Eleição atualizada com sucesso!";
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
            await LoadCompanies();
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
                    TempData["SuccessMessage"] = "Eleição excluída com sucesso!";
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

        [HttpPost]
        [RestrictWhenSealed]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var client = CreateAuthorizedClient();
            var updateStatusData = new { status = status };
            var content = new StringContent(JsonConvert.SerializeObject(updateStatusData), Encoding.UTF8, "application/json");
            var response = await client.PatchAsync($"{_apiBaseUrl}/{id}/status", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<object>>(responseContent);
                if (apiResponse != null && apiResponse.Success)
                {
                    var message = "Status da eleição atualizado com sucesso!";
                    if (status?.ToLower() == "completed")
                    {
                        message += " A eleição foi automaticamente lacrada.";
                        // Limpar cache do serviço de eleições lacradas para refletir mudança
                        _sealedElectionService.ClearCache();
                    }
                    TempData["SuccessMessage"] = message;
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

        [HttpPost]
        public async Task<IActionResult> SealElection(int id)
        {
            try
            {
                System.Console.WriteLine($"[DEBUG] SealElection called with ID: {id}");
                System.Console.WriteLine($"[DEBUG] User authenticated: {User.Identity?.IsAuthenticated}");
                
                var client = CreateAuthorizedClient();
                var token = HttpContext.Session.GetString("AccessToken");
                System.Console.WriteLine($"[DEBUG] Token available: {!string.IsNullOrEmpty(token)}");
                
                // Check seal status using election API
                var sealStatusResponse = await client.GetAsync($"{_apiBaseUrl}/{id}/seal/status");
                if (sealStatusResponse.IsSuccessStatusCode)
                {
                    var sealContent = await sealStatusResponse.Content.ReadAsStringAsync();
                    var sealStatus = JsonConvert.DeserializeObject<Models.ApiResponse<ElectionSealStatusDto>>(sealContent);
                    if (sealStatus?.Success == true && sealStatus.Data?.IsSealed == true)
                    {
                        return Json(new { success = false, message = "Esta eleição já foi lacrada anteriormente. Cada eleição pode ser lacrada apenas uma vez." });
                    }
                }
                else
                {
                    System.Console.WriteLine($"[DEBUG] Seal status check failed: {sealStatusResponse.StatusCode}");
                }
                
                System.Console.WriteLine($"[DEBUG] Calling API: http://localhost:5110/api/SystemSeal/generate/{id}");
                
                // Use the new seal endpoint from API documentation
                var response = await client.PostAsync($"{_apiBaseUrl}/{id}/seal", null);

                System.Console.WriteLine($"[DEBUG] API Response Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"[DEBUG] API Success Response: {responseContent}");
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<object>>(responseContent);
                    
                    // Limpar cache do serviço de eleições lacradas para forçar nova verificação
                    System.Console.WriteLine($"[DEBUG] Clearing sealed elections cache after successful seal");
                    _sealedElectionService.ClearCache();
                    
                    return Json(new { success = apiResponse?.Success ?? false, message = apiResponse?.Message ?? "Sistema lacrado com sucesso!" });
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                System.Console.WriteLine($"[DEBUG] API Error Response: {errorContent}");
                return Json(new { success = false, message = $"Erro da API: {response.StatusCode} - {errorContent}" });
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[DEBUG] Exception in SealElection: {ex.Message}");
                return Json(new { success = false, message = "Erro interno ao selar a eleição: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSealedElectionsStatus()
        {
            try
            {
                var client = CreateAuthorizedClient();
                // Check for elections with system seals (indicating they're sealed)
                var response = await client.GetAsync($"http://localhost:5110/api/election?page=1&limit=1000");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.ElectionListResponse>>(responseContent);
                    
                    if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                    {
                        var sealedElections = new List<int>();
                        var hasSealedElections = false;
                        
                        // Verificar se há eleições seladas na resposta (sealed ou completed)
                        if (apiResponse.Data.Items != null)
                        {
                            foreach (var item in apiResponse.Data.Items)
                            {
                                var status = item.Status?.ToLower();
                                if (status == "sealed" || status == "completed")
                                {
                                    sealedElections.Add(item.Id);
                                    hasSealedElections = true;
                                }
                            }
                        }
                        
                        return Json(new { 
                            hasSealedElections = hasSealedElections,
                            sealedElectionIds = sealedElections
                        });
                    }
                }
                
                // Default to no sealed elections if API call fails
                return Json(new { hasSealedElections = false, sealedElectionIds = new List<int>() });
            }
            catch (Exception)
            {
                // Default to no sealed elections if there's an exception
                return Json(new { hasSealedElections = false, sealedElectionIds = new List<int>() });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetElectionsForSealing()
        {
            try
            {
                var client = CreateAuthorizedClient();
                var allElections = new List<ElectionForSealingDto>();

                // Buscar eleições com diferentes status que podem ser lacradas
                var statuses = new[] { "draft", "scheduled", "active" };
                
                foreach (var status in statuses)
                {
                    var response = await client.GetAsync($"{_apiBaseUrl}?page=1&limit=50&status={status}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.ElectionListResponse>>(content);
                        
                        if (apiResponse?.Success == true && apiResponse.Data?.Items != null)
                        {
                            var elections = apiResponse.Data.Items.Select(e => new Models.ElectionForSealingDto
                            { 
                                Id = e.Id, 
                                Title = e.Title, 
                                Status = e.Status 
                            });
                            allElections.AddRange(elections);
                        }
                    }
                }
                
                // Remover duplicatas (se houver) e ordenar por título
                var uniqueElections = allElections
                    .GroupBy(e => e.Id)
                    .Select(g => g.First())
                    .OrderBy(e => e.Title)
                    .Select(e => new { id = e.Id, title = e.Title, status = e.Status })
                    .ToList();
                
                return Json(uniqueElections);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[ERROR] GetElectionsForSealing: {ex.Message}");
                return Json(new List<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetActive()
        {
            var client = CreateAuthorizedClient();
            var response = await client.GetAsync($"{_apiBaseUrl}/active");

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<List<Models.ElectionModel>>>(responseContent);
                if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                {
                    return Json(apiResponse.Data);
                }
            }
            return Json(new List<Models.ElectionModel>());
        }

        [HttpPost]
        public async Task<IActionResult> ValidateSeal(int id)
        {
            var client = CreateAuthorizedClient();
            var response = await client.PostAsync($"{_apiBaseUrl}/{id}/seal/validate", null);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<bool>>(responseContent);
                if (apiResponse != null)
                {
                    return Json(new { success = apiResponse.Success, isValid = apiResponse.Data, message = apiResponse.Message });
                }
            }
            return Json(new { success = false, isValid = false, message = "Erro ao validar lacre da eleição" });
        }

        
        private async Task LoadCompanies()
        {
            try
            {
                var client = CreateAuthorizedClient();
                var response = await client.GetAsync("http://localhost:5110/api/company");
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<List<Models.CompanyModel>>>(responseContent);
                    
                    if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                    {
                        ViewBag.Companies = apiResponse.Data.Select(c => new { 
                            Value = c.Id, 
                            Text = c.NomeFantasia 
                        }).ToList();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[DEBUG] Error loading companies: {ex.Message}");
            }
            
            // Fallback empty list
            ViewBag.Companies = new List<object>();
        }
    }

}