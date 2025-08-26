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
    [Authorize(Roles = "admin")] // Only admin can access candidate management
    public class CandidateController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiBaseUrl = "http://localhost:5110/api/candidate";
        private readonly string _electionApiBaseUrl = "http://localhost:5110/api/election";
        private readonly string _positionApiBaseUrl = "http://localhost:5110/api/position";

        public CandidateController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
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
        public async Task<IActionResult> List(string search = "", int? positionId = null, bool? isActive = null, int page = 1, int limit = 10)
        {
            var client = CreateAuthorizedClient();
            var url = $"{_apiBaseUrl}?page={page}&limit={limit}";

            if (!string.IsNullOrEmpty(search))
            {
                url += $"&search={search}";
            }
            if (positionId.HasValue)
            {
                url += $"&positionId={positionId.Value}";
            }
            if (isActive.HasValue)
            {
                url += $"&isActive={isActive.Value}";
            }

            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
                
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.CandidateListResponse>>(responseContent);
                if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                {
                    var viewModel = new Models.CandidateListViewModel
                    {
                        Candidates = apiResponse.Data.Items,
                        Search = search,
                        PositionId = positionId,
                        IsActive = isActive,
                        CurrentPage = apiResponse.Data.CurrentPage,
                        TotalPages = apiResponse.Data.TotalPages,
                        HasNextPage = apiResponse.Data.HasNextPage,
                        HasPreviousPage = apiResponse.Data.HasPreviousPage
                    };

                    // Fetch positions for filter dropdown
                    await PopulatePositionsDropdown(positionId);

                    return View(viewModel);
                }
                ModelState.AddModelError(string.Empty, apiResponse?.Message ?? "An unknown error occurred.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, $"API Error: {response.StatusCode}");
            }
            return View(new Models.CandidateListViewModel { Candidates = new List<Models.CandidateModel>() });
        }

        [HttpGet]
        [RestrictWhenSealed]
        public async Task<IActionResult> Create()
        {
            await PopulateElectionAndPositionDropdowns();
            return View();
        }

        [HttpPost]
        [RestrictWhenSealed]
        public async Task<IActionResult> Create(Models.CandidateModel model)
        {
            if (ModelState.IsValid)
            {
                var client = CreateAuthorizedClient();
                var requestData = new
                {
                    name = model.Name,
                    number = model.Number,
                    description = model.Description,
                    biography = model.Biography,
                    positionId = model.PositionId,
                    orderPosition = model.OrderPosition,
                    isActive = model.IsActive
                };

                // Debug log
                System.Console.WriteLine($"[DEBUG] Creating candidate with data: {JsonConvert.SerializeObject(requestData)}");
                System.Console.WriteLine($"[DEBUG] API URL: {_apiBaseUrl}");
                System.Console.WriteLine($"[DEBUG] Token present: {client.DefaultRequestHeaders.Authorization != null}");

                var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(_apiBaseUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.CandidateModel>>(responseContent);
                    if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                    {
                        TempData["SuccessMessage"] = "Candidato criado com sucesso!";
                        if (model.PhotoFile != null)
                        {
                            await UploadPhoto(apiResponse.Data.Id, model.PhotoFile);
                        }
                        return RedirectToAction("List");
                    }
                    ModelState.AddModelError(string.Empty, apiResponse?.Message ?? "An unknown error occurred.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"[DEBUG] Create Candidate API Error: Status={response.StatusCode}, Content={errorContent}");
                    ModelState.AddModelError(string.Empty, $"API Error: {response.StatusCode} - {errorContent}");
                }
            }
            await PopulateElectionAndPositionDropdowns();
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
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.CandidateModel>>(responseContent);
                if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                {
                    await PopulateElectionAndPositionDropdowns(apiResponse.Data.PositionId);
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
        public async Task<IActionResult> Edit(int id, Models.CandidateModel model)
        {
            if (ModelState.IsValid)
            {
                var client = CreateAuthorizedClient();
                var content = new StringContent(JsonConvert.SerializeObject(new
                {
                    name = model.Name,
                    number = model.Number,
                    description = model.Description,
                    biography = model.Biography,
                    positionId = model.PositionId,
                    orderPosition = model.OrderPosition,
                    isActive = model.IsActive
                }), Encoding.UTF8, "application/json");

                var response = await client.PutAsync($"{_apiBaseUrl}/{id}", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.CandidateModel>>(responseContent);
                    if (apiResponse != null && apiResponse.Success)
                    {
                        TempData["SuccessMessage"] = "Candidato atualizado com sucesso!";
                        if (model.PhotoFile != null)
                        {
                            await UploadPhoto(id, model.PhotoFile);
                        }
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
            await PopulateElectionAndPositionDropdowns(model.PositionId);
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
                    TempData["SuccessMessage"] = "Candidato excluído com sucesso!";
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

        private async Task PopulateElectionAndPositionDropdowns(int? selectedPositionId = null)
        {
            var client = CreateAuthorizedClient();

            // Fetch Elections
            var electionsResponse = await client.GetAsync($"{_electionApiBaseUrl}");
            if (electionsResponse.IsSuccessStatusCode)
            {
                var electionsContent = await electionsResponse.Content.ReadAsStringAsync();
                var electionsApiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.ElectionListResponse>>(electionsContent);
                if (electionsApiResponse != null && electionsApiResponse.Success && electionsApiResponse.Data != null)
                {
                    ViewBag.Elections = new SelectList(electionsApiResponse.Data.Items, "Id", "Title");
                }
            }

            // Fetch Positions
            var positionsResponse = await client.GetAsync($"{_positionApiBaseUrl}");
            if (positionsResponse.IsSuccessStatusCode)
            {
                var positionsContent = await positionsResponse.Content.ReadAsStringAsync();
                var positionsApiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.PositionListResponse>>(positionsContent);
                if (positionsApiResponse != null && positionsApiResponse.Success && positionsApiResponse.Data != null)
                {
                    ViewBag.Positions = new SelectList(positionsApiResponse.Data.Items, "Id", "Title", selectedPositionId);
                }
            }
        }

        private async Task PopulatePositionsDropdown(int? selectedPositionId = null)
        {
            var client = CreateAuthorizedClient();
            var positionsResponse = await client.GetAsync($"{_positionApiBaseUrl}");
            if (positionsResponse.IsSuccessStatusCode)
            {
                var positionsContent = await positionsResponse.Content.ReadAsStringAsync();
                var positionsApiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.PositionListResponse>>(positionsContent);
                if (positionsApiResponse != null && positionsApiResponse.Success && positionsApiResponse.Data != null)
                {
                    ViewBag.Positions = new SelectList(positionsApiResponse.Data.Items, "Id", "Title", selectedPositionId);
                }
            }
        }

        private async Task UploadPhoto(int candidateId, IFormFile photoFile)
        {
            Console.WriteLine($"[DEBUG] Entering UploadPhoto for CandidateId: {candidateId}, FileName: {photoFile.FileName}");
            var client = CreateAuthorizedClient();
            using (var formData = new MultipartFormDataContent())
            {
                formData.Add(new StreamContent(photoFile.OpenReadStream()), "photo", photoFile.FileName);
                
                try
                {
                    var response = await client.PostAsync($"{_apiBaseUrl}/{candidateId}/upload-photo-blob", formData);

                    Console.WriteLine($"[DEBUG] Photo upload response status: {response.StatusCode}");

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"[DEBUG] Photo upload error content: {errorContent}");
                        TempData["ErrorMessage"] = $"Erro ao fazer upload da foto: {response.StatusCode} - {errorContent}";
                    }
                    else
                    {
                        var successContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"[DEBUG] Photo upload success content: {successContent}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Exception during photo upload: {ex.Message}");
                    TempData["ErrorMessage"] = $"Ocorreu um erro inesperado ao fazer upload da foto: {ex.Message}";
                }
            }
        }

        [HttpGet("candidate/photo/{id}")]
        [AllowAnonymous] // This allows the browser to fetch the image without a cookie
        public async Task<IActionResult> GetPhoto(int id)
        {
            var client = CreateAuthorizedClient();
            var response = await client.GetAsync($"{_apiBaseUrl}/{id}/photo");

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.PhotoResponse>>(responseContent);

                if (apiResponse != null && apiResponse.Success && apiResponse.Data != null && apiResponse.Data.HasPhoto)
                {
                    if (apiResponse.Data.StorageType == "blob" && !string.IsNullOrEmpty(apiResponse.Data.PhotoUrl))
                    {
                        var base64Data = apiResponse.Data.PhotoUrl.Split(',')[1];
                        var bytes = Convert.FromBase64String(base64Data);
                        return File(bytes, apiResponse.Data.MimeType);
                    }
                    else if (apiResponse.Data.StorageType == "file" && !string.IsNullOrEmpty(apiResponse.Data.FullUrl))
                    {
                        // As the API is on localhost, we can't directly redirect.
                        // We need to fetch the image and stream it.
                        var photoResponse = await client.GetAsync(apiResponse.Data.FullUrl);
                        if (photoResponse.IsSuccessStatusCode)
                        {
                            var photoStream = await photoResponse.Content.ReadAsStreamAsync();
                            return File(photoStream, photoResponse.Content.Headers.ContentType?.ToString() ?? "application/octet-stream");
                        }
                    }
                }
            }
            // Return a default placeholder image if anything fails
            return File("~/images/placeholder.png", "image/png");
        }
    }
}
