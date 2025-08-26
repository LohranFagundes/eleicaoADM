using Microsoft.AspNetCore.Mvc;
using ElectionAdminPanel.Web.Models;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using ElectionAdminPanel.Web.Filters;

namespace ElectionAdminPanel.Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class CompanyController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiBaseUrl = "http://localhost:5110/api/company";

        public CompanyController(IHttpClientFactory httpClientFactory)
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

        public async Task<IActionResult> List(bool includeInactive = false)
        {
            var client = CreateAuthorizedClient();
            var url = $"{_apiBaseUrl}?includeInactive={includeInactive.ToString().ToLower()}";
            
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<List<Models.CompanyModel>>>(responseContent);
                if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                {
                    ViewBag.IncludeInactive = includeInactive;
                    return View(apiResponse.Data);
                }
                ModelState.AddModelError(string.Empty, apiResponse?.Message ?? "An unknown error occurred.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, $"API Error: {response.StatusCode}");
            }
            return View(new List<Models.CompanyModel>());
        }

        [HttpGet]
        [RestrictWhenSealed]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [RestrictWhenSealed]
        public async Task<IActionResult> Create(Models.CompanyModel model)
        {
            if (ModelState.IsValid)
            {
                var client = CreateAuthorizedClient();
                var requestData = new
                {
                    name = model.NomeFantasia,
                    cnpj = model.Cnpj,
                    email = model.Email,
                    phone = model.Phone,
                    address = $"{model.Logradouro}, {model.Numero}",
                    city = model.Cidade,
                    state = model.Estado,
                    zipCode = model.Cep,
                    isActive = true
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(_apiBaseUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.CompanyModel>>(responseContent);
                    if (apiResponse != null && apiResponse.Success)
                    {
                        TempData["SuccessMessage"] = "Empresa criada com sucesso!";
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
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.CompanyModel>>(responseContent);
                if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                {
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
        public async Task<IActionResult> Edit(int id, Models.CompanyModel model)
        {
            if (ModelState.IsValid)
            {
                var client = CreateAuthorizedClient();
                var requestData = new
                {
                    name = model.NomeFantasia,
                    cnpj = model.Cnpj,
                    email = model.Email,
                    phone = model.Phone,
                    address = $"{model.Logradouro}, {model.Numero}",
                    city = model.Cidade,
                    state = model.Estado,
                    zipCode = model.Cep,
                    isActive = model.IsActive
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
                var response = await client.PutAsync($"{_apiBaseUrl}/{id}", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<Models.CompanyModel>>(responseContent);
                    if (apiResponse != null && apiResponse.Success)
                    {
                        TempData["SuccessMessage"] = "Empresa atualizada com sucesso!";
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
                    TempData["SuccessMessage"] = "Empresa excluída com sucesso!";
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
        public async Task<IActionResult> UploadLogo(int id, IFormFile logoFile)
        {
            if (logoFile != null && logoFile.Length > 0)
            {
                var client = CreateAuthorizedClient();
                using var content = new MultipartFormDataContent();
                using var fileStream = logoFile.OpenReadStream();
                using var streamContent = new StreamContent(fileStream);
                content.Add(streamContent, "logoFile", logoFile.FileName);

                var response = await client.PostAsync($"{_apiBaseUrl}/{id}/logo", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Logo enviado com sucesso!";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["ErrorMessage"] = $"API Error: {response.StatusCode} - {errorContent}";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Nenhum arquivo selecionado.";
            }
            return RedirectToAction("Edit", new { id });
        }

        [HttpPost]
        [RestrictWhenSealed]
        public async Task<IActionResult> DeleteLogo(int id)
        {
            var client = CreateAuthorizedClient();
            var response = await client.DeleteAsync($"{_apiBaseUrl}/{id}/logo");

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Logo excluído com sucesso!";
            }
            else
            {
                TempData["ErrorMessage"] = $"API Error: {response.StatusCode}";
            }
            return RedirectToAction("Edit", new { id });
        }

        [HttpGet]
        public async Task<IActionResult> GetCompaniesForDropdown()
        {
            var client = CreateAuthorizedClient();
            var response = await client.GetAsync($"{_apiBaseUrl}?includeInactive=false");

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<List<Models.CompanyModel>>>(responseContent);
                if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                {
                    var companies = apiResponse.Data.Select(c => new { 
                        value = c.Id, 
                        text = c.NomeFantasia 
                    }).ToList();
                    return Json(companies);
                }
            }
            return Json(new List<object>());
        }
    }
}