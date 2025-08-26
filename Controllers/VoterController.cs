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
    [Authorize(Roles = "admin")] // Only admin can access voter management
    public class VoterController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly ISealedElectionService _sealedElectionService;
        private readonly string _apiBaseUrl = "http://localhost:5110/api/voter";

        public VoterController(IHttpClientFactory httpClientFactory, IEmailTemplateService emailTemplateService, ISealedElectionService sealedElectionService)
        {
            _httpClientFactory = httpClientFactory;
            _emailTemplateService = emailTemplateService;
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
        public async Task<IActionResult> List(string search = "", int page = 1, int limit = 10, bool? isActive = null, bool? isVerified = null)
        {
            var client = CreateAuthorizedClient();
            var url = $"{_apiBaseUrl}?page={page}&limit={limit}";

            if (!string.IsNullOrEmpty(search))
            {
                url += $"&search={search}";
            }

            if (isActive.HasValue)
            {
                url += $"&isActive={isActive.Value.ToString().ToLower()}";
            }

            if (isVerified.HasValue)
            {
                url += $"&isVerified={isVerified.Value.ToString().ToLower()}";
            }

            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<VoterListResponse>>(responseContent);
                if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                {
                    ViewBag.Search = search;
                    ViewBag.CurrentPage = apiResponse.Data.CurrentPage;
                    ViewBag.TotalPages = apiResponse.Data.TotalPages;
                    ViewBag.HasNextPage = apiResponse.Data.HasNextPage;
                    ViewBag.HasPreviousPage = apiResponse.Data.HasPreviousPage;
                    ViewBag.IsActive = isActive;
                    ViewBag.IsVerified = isVerified;
                    return View(apiResponse.Data.Items);
                }
                ModelState.AddModelError(string.Empty, apiResponse?.Message ?? "An unknown error occurred.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, $"API Error: {response.StatusCode}");
            }
            return View(new List<VoterModel>());
        }

        [HttpGet]
        [RestrictWhenSealed]
        public IActionResult Create()
        {
            return View(new VoterModel());
        }

        [HttpPost]
        [RestrictWhenSealed]
        public async Task<IActionResult> Create(VoterModel model)
        {
            if (ModelState.IsValid)
            {
                var client = CreateAuthorizedClient();
                var createData = new
                {
                    name = model.Name,
                    email = model.Email,
                    cpf = model.Cpf,
                    phone = model.Phone,
                    password = model.Password,
                    voteWeight = model.VoteWeight,
                    isActive = model.IsActive
                };

                var content = new StringContent(JsonConvert.SerializeObject(createData), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(_apiBaseUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<VoterModel>>(responseContent);
                    if (apiResponse != null && apiResponse.Success)
                    {
                        TempData["SuccessMessage"] = "Eleitor criado com sucesso!";
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
        public async Task<IActionResult> Edit(int id)
        {
            var client = CreateAuthorizedClient();
            var response = await client.GetAsync($"{_apiBaseUrl}/{id}");

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<VoterModel>>(responseContent);
                if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                {
                    // Pass sealed election information to view
                    ViewBag.HasSealedElections = await _sealedElectionService.HasSealedElectionsAsync();
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
        public async Task<IActionResult> Edit(int id, VoterModel model)
        {
            if (ModelState.IsValid)
            {
                var client = CreateAuthorizedClient();
                // Send all editable fields from API documentation
                var updateData = new 
                {
                    name = model.Name,
                    email = model.Email, 
                    cpf = model.Cpf,
                    phone = model.Phone,
                    voteWeight = model.VoteWeight,
                    isActive = model.IsActive
                };
                var content = new StringContent(JsonConvert.SerializeObject(updateData), Encoding.UTF8, "application/json");
                var response = await client.PutAsync($"{_apiBaseUrl}/{id}", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<VoterModel>>(responseContent);
                    if (apiResponse != null && apiResponse.Success)
                    {
                        TempData["SuccessMessage"] = "Eleitor atualizado com sucesso!";
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
                    TempData["SuccessMessage"] = "Eleitor excluído com sucesso!";
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

        [HttpGet]
        public async Task<IActionResult> GetStatistics()
        {
            var client = CreateAuthorizedClient();
            var response = await client.GetAsync($"{_apiBaseUrl}/statistics");

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<VoterStatisticsDto>>(responseContent);
                if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                {
                    return Json(apiResponse.Data);
                }
            }
            return Json(new VoterStatisticsDto());
        }

        [HttpPost]
        public async Task<IActionResult> SendVerificationEmail(int id)
        {
            var client = CreateAuthorizedClient();
            var response = await client.PostAsync($"{_apiBaseUrl}/{id}/send-verification", null);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<object>>(responseContent);
                return Json(new { success = apiResponse?.Success ?? false, message = apiResponse?.Message ?? "Email de verificação enviado com sucesso!" });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = $"Erro ao enviar email de verificação: {response.StatusCode} - {errorContent}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword([FromBody] VoterResetPasswordRequest request)
        {
            var client = CreateAuthorizedClient();
            var requestData = new
            {
                email = request.Email,
                newPassword = request.NewPassword
            };
            
            var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{_apiBaseUrl}/reset-password", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<object>>(responseContent);
                return Json(new { success = apiResponse?.Success ?? false, message = apiResponse?.Message ?? "Senha resetada com sucesso!" });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = $"Erro ao resetar senha: {response.StatusCode} - {errorContent}" });
            }
        }

        [HttpPost]
        [RestrictWhenSealed]
        public async Task<IActionResult> SendMassEmail([FromBody] MassEmailRequest request)
        {
            try
            {
                var client = CreateAuthorizedClient();
                
                // Converter para o formato esperado pela API
                var apiRequest = new
                {
                    subject = request.Subject,
                    body = request.Body,
                    isHtml = request.IsHtml,
                    target = new
                    {
                        sendToAllActiveVoters = request.Target.SendToAllActiveVoters,
                        sendToAllVerifiedVoters = request.Target.SendToAllVerifiedVoters,
                        specificVoterIds = request.Target.SpecificVoterIds,
                        specificEmails = request.Target.SpecificEmails
                    },
                    attachments = request.Attachments
                };
                
                var content = new StringContent(JsonConvert.SerializeObject(apiRequest), Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"http://localhost:5110/api/email/send-bulk", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<object>>(responseContent);
                    
                    return Json(new { success = apiResponse?.Success ?? false, message = apiResponse?.Message ?? "Emails enviados com sucesso!" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = $"Erro ao conectar com a API. Status: {response.StatusCode}" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro interno: {ex.Message}" });
            }
        }

        [HttpPost]
        [RestrictWhenSealed]
        public async Task<IActionResult> SendIndividualEmail([FromBody] IndividualEmailRequest request)
        {
            try
            {
                var client = CreateAuthorizedClient();
                
                // Converter para o formato esperado pela API /api/email/send
                var apiRequest = new
                {
                    toEmail = request.ToEmail,
                    toName = request.ToName,
                    subject = request.Subject,
                    body = request.Body,
                    isHtml = request.IsHtml,
                    attachments = request.Attachments
                };
                
                var content = new StringContent(JsonConvert.SerializeObject(apiRequest), Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"http://localhost:5110/api/email/send", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<object>>(responseContent);
                    
                    return Json(new { success = apiResponse?.Success ?? false, message = apiResponse?.Message ?? "Email enviado com sucesso!" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = $"Erro ao conectar com a API. Status: {response.StatusCode}" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro interno: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendPasswordReset([FromBody] PasswordResetRequest request)
        {
            try
            {
                var client = CreateAuthorizedClient();
                
                // Usar endpoint correto da API para solicitar reset de senha
                var forgotPasswordRequest = new
                {
                    email = request.ToEmail
                };
                
                var content = new StringContent(JsonConvert.SerializeObject(forgotPasswordRequest), Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"http://localhost:5110/api/voter/forgot-password", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<object>>(responseContent);
                    
                    return Json(new { 
                        success = apiResponse?.Success ?? false, 
                        message = apiResponse?.Message ?? "Link de redefinição enviado com sucesso!"
                    });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    
                    // Tentar deserializar a mensagem de erro da API
                    try
                    {
                        var errorResponse = JsonConvert.DeserializeObject<Models.ApiResponse<object>>(errorContent);
                        return Json(new { 
                            success = false, 
                            message = errorResponse?.Message ?? $"Erro na API: {response.StatusCode}" 
                        });
                    }
                    catch
                    {
                        return Json(new { 
                            success = false, 
                            message = $"Erro ao conectar com a API. Status: {response.StatusCode}" 
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro interno: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendMassPasswordReset()
        {
            try
            {
                var client = CreateAuthorizedClient();
                
                // Usar endpoint da API para redefinição de senha em massa
                // Primeiro, buscar todos os eleitores ativos
                var votersResponse = await client.GetAsync($"http://localhost:5110/api/voter?isActive=true&limit=1000");
                
                if (!votersResponse.IsSuccessStatusCode)
                {
                    return Json(new { 
                        success = false, 
                        message = "Erro ao buscar lista de eleitores ativos" 
                    });
                }
                
                var votersContent = await votersResponse.Content.ReadAsStringAsync();
                var votersApiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<VoterListResponse>>(votersContent);
                
                if (votersApiResponse?.Data?.Items == null || !votersApiResponse.Data.Items.Any())
                {
                    return Json(new { 
                        success = false, 
                        message = "Nenhum eleitor ativo encontrado" 
                    });
                }
                
                var successCount = 0;
                var errorCount = 0;
                var errors = new List<string>();
                
                // Enviar solicitação de reset para cada eleitor
                foreach (var voter in votersApiResponse.Data.Items)
                {
                    try
                    {
                        var forgotPasswordRequest = new { email = voter.Email };
                        var content = new StringContent(
                            JsonConvert.SerializeObject(forgotPasswordRequest), 
                            Encoding.UTF8, 
                            "application/json"
                        );
                        
                        var resetResponse = await client.PostAsync(
                            $"http://localhost:5110/api/voter/forgot-password", 
                            content
                        );
                        
                        if (resetResponse.IsSuccessStatusCode)
                        {
                            successCount++;
                        }
                        else
                        {
                            errorCount++;
                            errors.Add($"Erro ao enviar para {voter.Email}: {resetResponse.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"Erro ao enviar para {voter.Email}: {ex.Message}");
                    }
                }
                
                var message = $"Emails de redefinição enviados com sucesso para {successCount} eleitores.";
                if (errorCount > 0)
                {
                    message += $" {errorCount} falharam.";
                }
                
                return Json(new { 
                    success = successCount > 0, 
                    message = message,
                    successCount = successCount,
                    errorCount = errorCount,
                    errors = errorCount > 0 ? errors : null
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro interno: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> TestEmailConfiguration([FromBody] EmailTestRequest request)
        {
            try
            {
                var client = CreateAuthorizedClient();
                
                // Converter para o formato esperado pela API /api/email/test
                var apiRequest = new
                {
                    toEmail = request.ToEmail,
                    toName = request.ToName ?? "Administrador de Teste"
                };
                
                var content = new StringContent(JsonConvert.SerializeObject(apiRequest), Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"http://localhost:5110/api/email/test", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<Models.ApiResponse<object>>(responseContent);
                    
                    return Json(new { success = apiResponse?.Success ?? false, message = apiResponse?.Message ?? "Email de teste enviado com sucesso!" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = $"Erro ao testar configuração de email. Status: {response.StatusCode}" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro interno: {ex.Message}" });
            }
        }
    }

    public class MassEmailRequest
    {
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsHtml { get; set; } = true;
        public EmailTarget Target { get; set; } = new EmailTarget();
        public List<string> Attachments { get; set; } = new List<string>();
    }

    public class EmailTarget
    {
        public bool SendToAllActiveVoters { get; set; } = true;
        public bool SendToAllVerifiedVoters { get; set; } = false;
        public List<int> SpecificVoterIds { get; set; } = new List<int>();
        public List<string> SpecificEmails { get; set; } = new List<string>();
    }

    public class IndividualEmailRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string ToName { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsHtml { get; set; } = true;
        public List<string> Attachments { get; set; } = new List<string>();
    }

    public class PasswordResetRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string ToName { get; set; } = string.Empty;
        public string Subject { get; set; } = "Reset de Senha";
        public string Body { get; set; } = "Solicitação de reset de senha.";
        public bool IsHtml { get; set; } = true;
        public List<string> Attachments { get; set; } = new List<string>();
    }

    public class EmailTestRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string ToName { get; set; } = string.Empty;
    }

    public class VoterResetPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class VoterStatisticsDto
    {
        public int TotalVoters { get; set; }
        public int ActiveVoters { get; set; }
        public int VerifiedVoters { get; set; }
        public int InactiveVoters { get; set; }
        public int UnverifiedVoters { get; set; }
    }
}