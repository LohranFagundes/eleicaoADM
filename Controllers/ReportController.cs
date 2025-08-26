using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using ElectionAdminPanel.Web.Models;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using OfficeOpenXml;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ElectionAdminPanel.Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class ReportController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ReportController> _logger;
        private readonly string _apiBaseUrl = "http://localhost:5110/api/report";
        
        private static string? _cachedToken;
        private static DateTime _tokenExpiration = DateTime.MinValue;

        public ReportController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<ReportController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        private HttpClient CreateAuthorizedClient()
        {
            var client = _httpClientFactory.CreateClient();
            // Only try to get token if User context is available (not in background services)
            if (User != null && HttpContext != null)
            {
                // Get token from session instead of claims to avoid HTTP 431 errors
                var token = HttpContext.Session.GetString("AccessToken");
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
            }
            return client;
        }

        private async Task<string?> GetServiceTokenAsync()
        {
            // Verificar se existe um token válido em cache
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiration)
            {
                _logger.LogInformation("Using cached token");
                return _cachedToken;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                
                var apiBaseUrl = _configuration["BackgroundService:ApiBaseUrl"];
                var adminEmail = _configuration["BackgroundService:AdminEmail"];
                var adminPassword = _configuration["BackgroundService:AdminPassword"];

                _logger.LogInformation($"[DEBUG] GetServiceToken - Getting new token for: {adminEmail}");

                var loginData = new
                {
                    email = adminEmail,
                    password = adminPassword
                };

                var loginJson = JsonConvert.SerializeObject(loginData);
                var loginContent = new StringContent(loginJson, Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"{apiBaseUrl}/api/auth/admin/login", loginContent);
                _logger.LogInformation($"[DEBUG] GetServiceToken - Login Response Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    dynamic? result = JsonConvert.DeserializeObject(responseContent);
                    
                    if (result?.data?.token != null)
                    {
                        // Cache o token por 50 minutos (JWT expira em 1 hora)
                        _cachedToken = result.data.token;
                        _tokenExpiration = DateTime.UtcNow.AddMinutes(50);
                        _logger.LogInformation("New token cached successfully");
                        
                        return _cachedToken;
                    }
                }
                
                _logger.LogError($"Login failed: {response.StatusCode} - {response.ReasonPhrase}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting service token: {ex.Message}");
                return null;
            }
        }

        private async Task<HttpClient> CreateServiceAuthorizedClientAsync()
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30); // Set timeout to prevent hanging
            
            var token = await GetServiceTokenAsync();
            
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                _logger.LogInformation("ZeresimaReportService: Successfully authenticated with API");
            }
            else
            {
                _logger.LogError("ZeresimaReportService: Failed to authenticate with API");
            }
            
            return client;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string userType = "")
        {
            var viewModel = new ReportViewModel
            {
                CurrentPage = page,
                PageSize = pageSize,
                UserType = userType
            };

            // Se um userType específico foi solicitado, carrega apenas esse tipo
            if (!string.IsNullOrEmpty(userType))
            {
                if (userType == "admin")
                {
                    viewModel.AdminLogs = await GetAuditLogs("admin", page, pageSize);
                    viewModel.AuditLogs = new AuditLogListResponse(); // Vazio para outras abas
                    viewModel.VoterLogs = new AuditLogListResponse(); // Vazio para outras abas
                }
                else if (userType == "voter")
                {
                    viewModel.VoterLogs = await GetAuditLogs("voter", page, pageSize);
                    viewModel.AuditLogs = new AuditLogListResponse(); // Vazio para outras abas
                    viewModel.AdminLogs = new AuditLogListResponse(); // Vazio para outras abas
                }
                else
                {
                    viewModel.AuditLogs = await GetAuditLogs("", page, pageSize);
                    viewModel.AdminLogs = new AuditLogListResponse(); // Vazio para outras abas
                    viewModel.VoterLogs = new AuditLogListResponse(); // Vazio para outras abas
                }
            }
            else
            {
                // Se nenhum userType específico, carrega todos os logs na primeira aba
                viewModel.AuditLogs = await GetAuditLogs("", page, pageSize);
                viewModel.AdminLogs = new AuditLogListResponse(); // Vazio inicialmente
                viewModel.VoterLogs = new AuditLogListResponse(); // Vazio inicialmente
            }

            return View(viewModel);
        }

        private async Task<AuditLogListResponse> GetAuditLogs(string userType, int page = 1, int pageSize = 10)
        {
            var client = CreateAuthorizedClient();
            var url = $"{_apiBaseUrl}/audit-logs?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(userType))
            {
                url += $"&userType={userType}";
            }

            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<AuditLogListResponse>>(responseContent);
                if (apiResponse.Success && apiResponse.Data != null)
                {
                    return apiResponse.Data;
                }
            }
            return new AuditLogListResponse();
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string userType = "", int page = 1, int pageSize = 1000)
        {
            var auditLogs = await GetAuditLogs(userType, page, pageSize);
            
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Audit Logs");

            worksheet.Cells[1, 1].Value = "ID";
            worksheet.Cells[1, 2].Value = "User ID";
            worksheet.Cells[1, 3].Value = "User Type";
            worksheet.Cells[1, 4].Value = "Action";
            worksheet.Cells[1, 5].Value = "Entity Type";
            worksheet.Cells[1, 6].Value = "Entity ID";
            worksheet.Cells[1, 7].Value = "Details";
            worksheet.Cells[1, 8].Value = "Timestamp";
            worksheet.Cells[1, 9].Value = "IP Address";

            using var range = worksheet.Cells[1, 1, 1, 9];
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

            for (int i = 0; i < auditLogs.Items.Count; i++)
            {
                var log = auditLogs.Items[i];
                var row = i + 2;
                
                worksheet.Cells[row, 1].Value = log.Id;
                worksheet.Cells[row, 2].Value = log.UserId;
                worksheet.Cells[row, 3].Value = log.UserType;
                worksheet.Cells[row, 4].Value = log.Action;
                worksheet.Cells[row, 5].Value = log.EntityType;
                worksheet.Cells[row, 6].Value = log.EntityId;
                worksheet.Cells[row, 7].Value = log.Details;
                worksheet.Cells[row, 8].Value = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cells[row, 9].Value = log.IpAddress;
            }

            worksheet.Cells.AutoFitColumns();

            var stream = new MemoryStream();
            package.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"audit_logs_{userType}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(string userType = "", int page = 1, int pageSize = 1000)
        {
            var auditLogs = await GetAuditLogs(userType, page, pageSize);
            
            using var stream = new MemoryStream();
            var document = new Document(PageSize.A4.Rotate(), 25, 25, 30, 30);
            var writer = PdfWriter.GetInstance(document, stream);
            
            document.Open();

            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 8);

            var title = new Paragraph($"Audit Logs Report{(!string.IsNullOrEmpty(userType) ? $" - {userType}" : "")}", titleFont);
            title.Alignment = Element.ALIGN_CENTER;
            title.SpacingAfter = 20;
            document.Add(title);

            var table = new PdfPTable(9);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 8, 12, 10, 15, 12, 8, 20, 15, 12 });

            var headers = new[] { "ID", "User ID", "User Type", "Action", "Entity Type", "Entity ID", "Details", "Timestamp", "IP Address" };
            foreach (var header in headers)
            {
                var cell = new PdfPCell(new Phrase(header, headerFont));
                cell.BackgroundColor = new BaseColor(192, 192, 192);
                cell.HorizontalAlignment = Element.ALIGN_CENTER;
                cell.Padding = 5;
                table.AddCell(cell);
            }

            foreach (var log in auditLogs.Items)
            {
                table.AddCell(new PdfPCell(new Phrase(log.Id.ToString(), cellFont)) { Padding = 3 });
                table.AddCell(new PdfPCell(new Phrase(log.UserId ?? "", cellFont)) { Padding = 3 });
                table.AddCell(new PdfPCell(new Phrase(log.UserType ?? "", cellFont)) { Padding = 3 });
                table.AddCell(new PdfPCell(new Phrase(log.Action ?? "", cellFont)) { Padding = 3 });
                table.AddCell(new PdfPCell(new Phrase(log.EntityType ?? "", cellFont)) { Padding = 3 });
                table.AddCell(new PdfPCell(new Phrase(log.EntityId?.ToString() ?? "", cellFont)) { Padding = 3 });
                table.AddCell(new PdfPCell(new Phrase(log.Details ?? "", cellFont)) { Padding = 3 });
                table.AddCell(new PdfPCell(new Phrase(log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), cellFont)) { Padding = 3 });
                table.AddCell(new PdfPCell(new Phrase(log.IpAddress ?? "", cellFont)) { Padding = 3 });
            }

            document.Add(table);
            document.Close();

            var fileName = $"audit_logs_{userType}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            return File(stream.ToArray(), "application/pdf", fileName);
        }

        [AllowAnonymous]
        public async Task  priciGenerateZeresimaReport()
        {
            try
            {
                _logger.LogInformation("Iniciando verificação de relatórios de zerésima...");
                
                var client = await CreateServiceAuthorizedClientAsync();
                var apiBaseUrl = _configuration["BackgroundService:ApiBaseUrl"];

                // Buscar eleições agendadas para iniciar em breve
                var response = await client.GetAsync($"{apiBaseUrl}/api/election?status=scheduled&page=1&limit=1000");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Falha ao buscar eleições agendadas: {response.StatusCode}");
                    return;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic apiResponse = JsonConvert.DeserializeObject(responseContent);
                List<ElectionModel>? elections = JsonConvert.DeserializeObject<List<ElectionModel>>(apiResponse?.data?.items?.ToString() ?? "[]");

                _logger.LogInformation($"Encontradas {elections?.Count ?? 0} eleições agendadas para verificação");

                foreach (var election in elections ?? new List<ElectionModel>())
                {
                    var timeUntilStart = election.StartDate - DateTime.UtcNow;
                    
                    // Verificar se falta exatamente 1 minuto (com tolerância de ±30 segundos)
                    if (timeUntilStart.TotalSeconds < 30 || timeUntilStart.TotalSeconds > 90)
                    {
                        continue;
                    }
                    
                    _logger.LogInformation($"Gerando relatório de zerésima para eleição {election.Id} - {election.Title}");

                    // 0. Verificar se a eleição já está lacrada usando election API
                    var sealStatusResponse = await client.GetAsync($"{apiBaseUrl}/api/election/{election.Id}/seal/status");
                    bool alreadySealed = false;
                    if (sealStatusResponse.IsSuccessStatusCode)
                    {
                        var sealStatusContent = await sealStatusResponse.Content.ReadAsStringAsync();
                        dynamic? sealStatusData = JsonConvert.DeserializeObject(sealStatusContent);
                        alreadySealed = sealStatusData?.data?.isSealed == true;
                    }

                    // Lacrar a eleição se ainda não estiver lacrada
                    if (!alreadySealed)
                    {
                        var sealResponse = await client.PostAsync($"{apiBaseUrl}/api/election/{election.Id}/seal", null);
                        if (!sealResponse.IsSuccessStatusCode)
                        {
                            _logger.LogError($"Failed to seal election {election.Id}. Status: {sealResponse.StatusCode}");
                            continue;
                        }
                        _logger.LogInformation($"Eleição {election.Id} lacrada com sucesso");
                    }
                    else
                    {
                        _logger.LogInformation($"Eleição {election.Id} já estava lacrada");
                    }

                    // Validate the election state after sealing
                    var validationResponse = await client.GetAsync($"{apiBaseUrl}/api/voting-portal/elections/{election.Id}/validate");
                    if(validationResponse.IsSuccessStatusCode)
                    {
                        var validationContent = await validationResponse.Content.ReadAsStringAsync();
                        _logger.LogInformation($"Validation status for election {election.Id} after seal attempt: {validationContent}");
                    }

                    // 1. Get Zeresima Report usando o endpoint correto
                    var zeresimaResponse = await client.PostAsync($"{apiBaseUrl}/api/voting/zero-report/{election.Id}", null);
                    if (!zeresimaResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError($"Failed to get zeresima report for election {election.Id}. Status: {zeresimaResponse.StatusCode}");
                        continue;
                    }
                    
                    var zeresimaContent = await zeresimaResponse.Content.ReadAsStringAsync();
                    dynamic? zeresimaApiResponse = JsonConvert.DeserializeObject(zeresimaContent);
                    var zeresimaReport = zeresimaApiResponse?.data;
                    
                    // Obter também o hash do lacre
                    var sealHashResponse = await client.GetAsync($"{apiBaseUrl}/api/election/{election.Id}/seal/status");
                    string sealHash = "N/A";
                    if (sealHashResponse.IsSuccessStatusCode)
                    {
                        var sealHashContent = await sealHashResponse.Content.ReadAsStringAsync();
                        dynamic? sealHashData = JsonConvert.DeserializeObject(sealHashContent);
                        sealHash = sealHashData?.data?.sealHash?.ToString() ?? "N/A";
                    }

                    // 2. Get Company Info
                    var companyResponse = await client.GetAsync($"{apiBaseUrl}/api/company/{election.CompanyId}");
                    dynamic? companyInfo = null;
                    if (companyResponse.IsSuccessStatusCode)
                    {
                        var companyContent = await companyResponse.Content.ReadAsStringAsync();
                        dynamic? companyApiResponse = JsonConvert.DeserializeObject(companyContent);
                        companyInfo = companyApiResponse?.data;
                    }

                    // 3. Generate PDF
                    byte[] pdfBytes = GenerateZeresimaPdfReport(zeresimaReport, companyInfo, election, sealHash);
                    _logger.LogInformation($"PDF de zerésima gerado com sucesso para eleição {election.Id}");

                    // 4. Send Email to all Admins
                    var adminResponse = await client.GetAsync($"{apiBaseUrl}/api/adminmanagement");
                    if (adminResponse.IsSuccessStatusCode)
                    {
                        var adminContent = await adminResponse.Content.ReadAsStringAsync();
                        dynamic? adminApiResponse = JsonConvert.DeserializeObject(adminContent);
                        var admins = JsonConvert.DeserializeObject<List<dynamic>>(adminApiResponse?.data?.items?.ToString() ?? "[]");

                        foreach (var admin in admins)
                        {
                            var emailData = new
                            {
                                toEmail = admin.email.ToString(),
                                toName = admin.name.ToString(),
                                subject = $"🔒 Relatório de Zerésima - {election.Title}",
                                body = $@"
                                    <h2>Relatório de Zerésima Gerado</h2>
                                    <p><strong>Eleição:</strong> {election.Title}</p>
                                    <p><strong>Data/Hora de Início:</strong> {election.StartDate:dd/MM/yyyy HH:mm}</p>
                                    <p><strong>Status:</strong> Lacrada para início</p>
                                    
                                    <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 15px 0;'>
                                        <p><strong>📋 Informações Importantes:</strong></p>
                                        <ul>
                                            <li>Este relatório comprova o estado inicial (zerésima) do sistema eleitoral</li>
                                            <li>A eleição foi automaticamente lacrada e está pronta para início</li>
                                            <li>Mantenha este documento para fins de auditoria</li>
                                        </ul>
                                    </div>
                                    
                                    <p>Em anexo encontra-se o relatório completo em PDF com:</p>
                                    <ul>
                                        <li>Dados da empresa</li>
                                        <li>Informações da eleição</li>
                                        <li>Contagem de eleitores e candidatos</li>
                                        <li>Status dos votos (zerésima)</li>
                                    </ul>
                                    
                                    <hr>
                                    <p style='font-size: 12px; color: #666;'>
                                        Este email foi gerado automaticamente pelo Sistema de Gestão Eleitoral.<br>
                                        Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm:ss}
                                    </p>
                                ",
                                isHtml = true,
                                attachments = new[]
                                {
                                    new {
                                        fileName = $"Zeresima_{election.Title.Replace(" ", "_").Replace("/", "-")}_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                                        fileContent = Convert.ToBase64String(pdfBytes)
                                    }
                                }
                            };
                            var emailJson = JsonConvert.SerializeObject(emailData);
                            var emailContent = new StringContent(emailJson, Encoding.UTF8, "application/json");
                            var emailResponse = await client.PostAsync($"{apiBaseUrl}/api/email/send", emailContent);
                            if(!emailResponse.IsSuccessStatusCode)
                            {
                                _logger.LogError($"Failed to send zeresima email to {admin.email}. Status: {emailResponse.StatusCode}");
                            }
                        }
                    }

                    // 5. Mark election as active
                    var statusUpdateData = new { status = "active" };
                    var statusUpdateJson = JsonConvert.SerializeObject(statusUpdateData);
                    var statusUpdateContent = new StringContent(statusUpdateJson, Encoding.UTF8, "application/json");
                    var patchResponse = await client.PatchAsync($"{apiBaseUrl}/api/election/{election.Id}/status", statusUpdateContent);
                    if(!patchResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError($"Failed to update election {election.Id} status to active. Status: {patchResponse.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in GenerateZeresimaReport.");
            }
        }

        private byte[] GenerateZeresimaPdfReport(dynamic reportData, dynamic companyData, ElectionModel election, string sealHash = "N/A")
        {
            using var stream = new MemoryStream();
            var document = new Document(PageSize.A4, 40, 40, 50, 50);
            var writer = PdfWriter.GetInstance(document, stream);
            document.Open();

            // Fonts
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 20, new BaseColor(64, 64, 64));
            var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, new BaseColor(64, 64, 64));
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, new BaseColor(0, 0, 0));
            var bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, new BaseColor(0, 0, 0));
            var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, new BaseColor(128, 128, 128));

            // Header with Company Info and Logo
            var headerTable = new PdfPTable(2);
            headerTable.WidthPercentage = 100;
            headerTable.SetWidths(new float[] { 75, 25 });

            // Company Info Cell
            var companyCell = new PdfPCell();
            companyCell.Border = Rectangle.NO_BORDER;
            companyCell.PaddingBottom = 10;

            if (companyData != null)
            {
                companyCell.AddElement(new Paragraph(companyData.razaoSocial?.ToString() ?? "Empresa não identificada", headerFont));
                
                if (companyData.cnpj != null)
                    companyCell.AddElement(new Paragraph($"CNPJ: {companyData.cnpj}", bodyFont));
                
                if (companyData.logradouro != null)
                {
                    var address = $"{companyData.logradouro}";
                    if (companyData.numero != null) address += $", {companyData.numero}";
                    if (companyData.bairro != null) address += $" - {companyData.bairro}";
                    if (companyData.cidade != null) address += $", {companyData.cidade}";
                    if (companyData.cep != null) address += $" - CEP: {companyData.cep}";
                    companyCell.AddElement(new Paragraph(address, bodyFont));
                }
                
                if (companyData.email != null)
                    companyCell.AddElement(new Paragraph($"Email: {companyData.email}", bodyFont));
                if (companyData.telefone != null)
                    companyCell.AddElement(new Paragraph($"Telefone: {companyData.telefone}", bodyFont));
            }
            else
            {
                companyCell.AddElement(new Paragraph("Informações da empresa não disponíveis", bodyFont));
            }
            headerTable.AddCell(companyCell);

            // Logo Cell (placeholder for now)
            var logoCell = new PdfPCell(new Paragraph("LOGO\nEMPRESA", headerFont) { Alignment = Element.ALIGN_CENTER });
            logoCell.Border = Rectangle.BOX;
            logoCell.BorderColor = new BaseColor(192, 192, 192);
            logoCell.HorizontalAlignment = Element.ALIGN_CENTER;
            logoCell.VerticalAlignment = Element.ALIGN_MIDDLE;
            logoCell.MinimumHeight = 80;
            headerTable.AddCell(logoCell);

            document.Add(headerTable);
            document.Add(new Paragraph(new Chunk("\n")));

            // Report Title
            var title = new Paragraph("RELATÓRIO DE ZERÉSIMA", titleFont);
            title.Alignment = Element.ALIGN_CENTER;
            title.SpacingAfter = 10;
            document.Add(title);

            var electionTitle = new Paragraph($"Eleição: {election.Title}", subtitleFont);
            electionTitle.Alignment = Element.ALIGN_CENTER;
            electionTitle.SpacingAfter = 5;
            document.Add(electionTitle);

            var dateInfo = new Paragraph($"Gerado em: {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")}", bodyFont);
            dateInfo.Alignment = Element.ALIGN_CENTER;
            dateInfo.SpacingAfter = 20;
            document.Add(dateInfo);

            // Election Details
            var detailsTable = new PdfPTable(2);
            detailsTable.WidthPercentage = 100;
            detailsTable.SetWidths(new float[] { 30, 70 });
            
            AddDetailRow(detailsTable, "Data de Início:", election.StartDate.ToString("dd/MM/yyyy HH:mm"), headerFont, bodyFont);
            AddDetailRow(detailsTable, "Data de Término:", election.EndDate.ToString("dd/MM/yyyy HH:mm"), headerFont, bodyFont);
            AddDetailRow(detailsTable, "Tipo de Eleição:", election.ElectionType, headerFont, bodyFont);
            AddDetailRow(detailsTable, "Status:", election.Status, headerFont, bodyFont);
            
            document.Add(detailsTable);
            document.Add(new Paragraph(new Chunk("\n")));

            // Summary Section
            var summaryTitle = new Paragraph("RESUMO ESTATÍSTICO", headerFont);
            summaryTitle.SpacingAfter = 10;
            document.Add(summaryTitle);

            var summaryTable = new PdfPTable(2);
            summaryTable.WidthPercentage = 100;
            summaryTable.SetWidths(new float[] { 60, 40 });

            if (reportData != null)
            {
                AddSummaryRow(summaryTable, "Total de Votos Registrados:", reportData.totalVotes?.ToString() ?? "0", headerFont, bodyFont);
                AddSummaryRow(summaryTable, "Total de Eleitores Aptos:", reportData.totalRegisteredVoters?.ToString() ?? "0", headerFont, bodyFont);
                AddSummaryRow(summaryTable, "Total de Cargos:", reportData.totalPositions?.ToString() ?? "0", headerFont, bodyFont);
                AddSummaryRow(summaryTable, "Total de Candidatos:", reportData.totalCandidates?.ToString() ?? "0", headerFont, bodyFont);
            }
            else
            {
                AddSummaryRow(summaryTable, "Status do Sistema:", "Zerésima - Sem votos registrados", headerFont, bodyFont);
                AddSummaryRow(summaryTable, "Observação:", "Relatório gerado antes do início da votação", headerFont, bodyFont);
            }

            document.Add(summaryTable);
            document.Add(new Paragraph(new Chunk("\n")));

            // Positions and Candidates (if available)
            if (reportData?.positions != null)
            {
                var positionsTitle = new Paragraph("DETALHAMENTO POR CARGO", headerFont);
                positionsTitle.SpacingAfter = 10;
                document.Add(positionsTitle);

                foreach (var position in reportData.positions)
                {
                    document.Add(new Paragraph($"Cargo: {position.positionName}", headerFont));
                    
                    var positionTable = new PdfPTable(3);
                    positionTable.WidthPercentage = 100;
                    positionTable.SetWidths(new float[] { 60, 20, 20 });
                    
                    // Header
                    var candidateHeader = new PdfPCell(new Phrase("Candidato", headerFont));
                    candidateHeader.BackgroundColor = new BaseColor(240, 240, 240);
                    candidateHeader.Padding = 5;
                    positionTable.AddCell(candidateHeader);

                    var numberHeader = new PdfPCell(new Phrase("Número", headerFont));
                    numberHeader.BackgroundColor = new BaseColor(240, 240, 240);
                    numberHeader.HorizontalAlignment = Element.ALIGN_CENTER;
                    numberHeader.Padding = 5;
                    positionTable.AddCell(numberHeader);

                    var votesHeader = new PdfPCell(new Phrase("Votos", headerFont));
                    votesHeader.BackgroundColor = new BaseColor(240, 240, 240);
                    votesHeader.HorizontalAlignment = Element.ALIGN_CENTER;
                    votesHeader.Padding = 5;
                    positionTable.AddCell(votesHeader);

                    // Candidates
                    foreach (var candidate in position.candidates)
                    {
                        positionTable.AddCell(new PdfPCell(new Phrase(candidate.candidateName?.ToString() ?? "N/A", bodyFont)) { Padding = 3 });
                        
                        var numberCell = new PdfPCell(new Phrase(candidate.candidateNumber?.ToString() ?? "N/A", bodyFont));
                        numberCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        numberCell.Padding = 3;
                        positionTable.AddCell(numberCell);
                        
                        var voteCell = new PdfPCell(new Phrase(candidate.voteCount?.ToString() ?? "0", bodyFont));
                        voteCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        voteCell.Padding = 3;
                        positionTable.AddCell(voteCell);
                    }
                    
                    document.Add(positionTable);
                    document.Add(new Paragraph(new Chunk("\n")));
                }
            }

            // Footer with seal hash
            document.Add(new Paragraph(new Chunk("\n\n")));
            var footer = new Paragraph("Este documento comprova o estado inicial (zerésima) do sistema eleitoral no momento de sua geração.", smallFont);
            footer.Alignment = Element.ALIGN_CENTER;
            footer.SpacingBefore = 20;
            document.Add(footer);

            var sealInfo = new Paragraph($"Hash do Lacre: {sealHash}", smallFont);
            sealInfo.Alignment = Element.ALIGN_CENTER;
            document.Add(sealInfo);

            var disclaimer = new Paragraph("Documento gerado automaticamente pelo Sistema de Gestão Eleitoral.", smallFont);
            disclaimer.Alignment = Element.ALIGN_CENTER;
            document.Add(disclaimer);

            document.Close();
            return stream.ToArray();
        }

        private void AddDetailRow(PdfPTable table, string label, string value, Font labelFont, Font valueFont)
        {
            var labelCell = new PdfPCell(new Phrase(label, labelFont));
            labelCell.Border = Rectangle.NO_BORDER;
            labelCell.Padding = 3;
            table.AddCell(labelCell);

            var valueCell = new PdfPCell(new Phrase(value ?? "N/A", valueFont));
            valueCell.Border = Rectangle.NO_BORDER;
            valueCell.Padding = 3;
            table.AddCell(valueCell);
        }

        private void AddSummaryRow(PdfPTable table, string label, string value, Font labelFont, Font valueFont)
        {
            var labelCell = new PdfPCell(new Phrase(label, labelFont));
            labelCell.Border = Rectangle.BOTTOM_BORDER;
            labelCell.BorderColor = new BaseColor(192, 192, 192);
            labelCell.Padding = 5;
            table.AddCell(labelCell);

            var valueCell = new PdfPCell(new Phrase(value ?? "0", valueFont));
            valueCell.Border = Rectangle.BOTTOM_BORDER;
            valueCell.BorderColor = new BaseColor(192, 192, 192);
            valueCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            valueCell.Padding = 5;
            table.AddCell(valueCell);
        }

        [AllowAnonymous]
        public async Task GenerateFinalReport()
        {
            try
            {
                _logger.LogInformation("Iniciando verificação de relatórios finais...");
                
                var client = await CreateServiceAuthorizedClientAsync();
                var apiBaseUrl = _configuration["BackgroundService:ApiBaseUrl"];

                // Buscar eleições ativas que terminaram há 1-3 minutos
                var response = await client.GetAsync($"{apiBaseUrl}/api/election?status=active&page=1&limit=1000");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Falha ao buscar eleições ativas: {response.StatusCode}");
                    return;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic apiResponse = JsonConvert.DeserializeObject(responseContent);
                List<ElectionModel>? elections = JsonConvert.DeserializeObject<List<ElectionModel>>(apiResponse?.data?.items?.ToString() ?? "[]");

                _logger.LogInformation($"Encontradas {elections?.Count ?? 0} eleições ativas para verificação");

                foreach (var election in elections ?? new List<ElectionModel>())
                {
                    var timeSinceEnd = DateTime.UtcNow - election.EndDate;
                    
                    // Verificar se terminou há 1-3 minutos
                    if (timeSinceEnd.TotalSeconds < 60 || timeSinceEnd.TotalSeconds > 180)
                    {
                        continue;
                    }
                    
                    _logger.LogInformation($"Gerando relatório final para eleição {election.Id} - {election.Title}");

                    // 1. Get Final Vote Report
                    var finalReportResponse = await client.GetAsync($"{apiBaseUrl}/api/voting/results/{election.Id}");
                    if (!finalReportResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError($"Failed to get final report for election {election.Id}. Status: {finalReportResponse.StatusCode}");
                        continue;
                    }
                    
                    var finalReportContent = await finalReportResponse.Content.ReadAsStringAsync();
                    dynamic? finalReportApiResponse = JsonConvert.DeserializeObject(finalReportContent);
                    var finalReport = finalReportApiResponse?.data;
                    
                    // 2. Obter o hash do lacre
                    var sealHashResponse = await client.GetAsync($"{apiBaseUrl}/api/election/{election.Id}/seal/status");
                    string sealHash = "N/A";
                    if (sealHashResponse.IsSuccessStatusCode)
                    {
                        var sealHashContent = await sealHashResponse.Content.ReadAsStringAsync();
                        dynamic? sealHashData = JsonConvert.DeserializeObject(sealHashContent);
                        sealHash = sealHashData?.data?.sealHash?.ToString() ?? "N/A";
                    }

                    // 3. Get Company Info
                    var companyResponse = await client.GetAsync($"{apiBaseUrl}/api/company/{election.CompanyId}");
                    dynamic? companyInfo = null;
                    if (companyResponse.IsSuccessStatusCode)
                    {
                        var companyContent = await companyResponse.Content.ReadAsStringAsync();
                        dynamic? companyApiResponse = JsonConvert.DeserializeObject(companyContent);
                        companyInfo = companyApiResponse?.data;
                    }

                    // 4. Generate PDF
                    byte[] pdfBytes = GenerateFinalPdfReport(finalReport, companyInfo, election, sealHash);
                    _logger.LogInformation($"PDF de relatório final gerado com sucesso para eleição {election.Id}");

                    // 5. Send Email to all Admins
                    var adminResponse = await client.GetAsync($"{apiBaseUrl}/api/adminmanagement");
                    if (adminResponse.IsSuccessStatusCode)
                    {
                        var adminContent = await adminResponse.Content.ReadAsStringAsync();
                        dynamic? adminApiResponse = JsonConvert.DeserializeObject(adminContent);
                        var admins = JsonConvert.DeserializeObject<List<dynamic>>(adminApiResponse?.data?.items?.ToString() ?? "[]");

                        foreach (var admin in admins)
                        {
                            var emailData = new
                            {
                                toEmail = admin.email.ToString(),
                                toName = admin.name.ToString(),
                                subject = $"📊 Relatório Final - {election.Title}",
                                body = $@"
                                    <h2>Relatório Final de Eleição</h2>
                                    <p><strong>Eleição:</strong> {election.Title}</p>
                                    <p><strong>Data/Hora de Término:</strong> {election.EndDate:dd/MM/yyyy HH:mm}</p>
                                    <p><strong>Status:</strong> Encerrada</p>
                                    
                                    <div style='background-color: #e8f5e8; padding: 15px; border-radius: 5px; margin: 15px 0;'>
                                        <p><strong>✅ Eleição Concluída com Sucesso!</strong></p>
                                        <ul>
                                            <li>Este relatório contém a contabilização final dos votos</li>
                                            <li>Todos os dados foram processados e validados</li>
                                            <li>Mantenha este documento para fins de auditoria</li>
                                        </ul>
                                    </div>
                                    
                                    <p>Em anexo encontra-se o relatório completo em PDF com:</p>
                                    <ul>
                                        <li>Resultados finais por cargo</li>
                                        <li>Contabilização de votos por candidato</li>
                                        <li>Estatísticas gerais da votação</li>
                                        <li>Hash do lacramento para verificação</li>
                                    </ul>
                                    
                                    <hr>
                                    <p style='font-size: 12px; color: #666;'>
                                        Este email foi gerado automaticamente pelo Sistema de Gestão Eleitoral.<br>
                                        Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm:ss}<br>
                                        Hash do Lacre: {sealHash}
                                    </p>
                                ",
                                isHtml = true,
                                attachments = new[]
                                {
                                    new {
                                        fileName = $"RelatorioFinal_{election.Title.Replace(" ", "_").Replace("/", "-")}_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                                        fileContent = Convert.ToBase64String(pdfBytes)
                                    }
                                }
                            };
                            var emailJson = JsonConvert.SerializeObject(emailData);
                            var emailContent = new StringContent(emailJson, Encoding.UTF8, "application/json");
                            var emailResponse = await client.PostAsync($"{apiBaseUrl}/api/email/send", emailContent);
                            if(!emailResponse.IsSuccessStatusCode)
                            {
                                _logger.LogError($"Failed to send final report email to {admin.email}. Status: {emailResponse.StatusCode}");
                            }
                        }
                    }

                    // 6. Mark election as completed
                    var statusUpdateData = new { status = "completed" };
                    var statusUpdateJson = JsonConvert.SerializeObject(statusUpdateData);
                    var statusUpdateContent = new StringContent(statusUpdateJson, Encoding.UTF8, "application/json");
                    var patchResponse = await client.PatchAsync($"{apiBaseUrl}/api/election/{election.Id}/status", statusUpdateContent);
                    if(!patchResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError($"Failed to update election {election.Id} status to completed. Status: {patchResponse.StatusCode}");
                    }
                    else
                    {
                        _logger.LogInformation($"Eleição {election.Id} marcada como concluída");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in GenerateFinalReport.");
            }
        }

        private byte[] GenerateFinalPdfReport(dynamic reportData, dynamic companyData, ElectionModel election, string sealHash = "N/A")
        {
            using var stream = new MemoryStream();
            var document = new Document(PageSize.A4, 40, 40, 50, 50);
            var writer = PdfWriter.GetInstance(document, stream);
            document.Open();

            // Fonts
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 20, new BaseColor(64, 64, 64));
            var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, new BaseColor(64, 64, 64));
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, new BaseColor(0, 0, 0));
            var bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, new BaseColor(0, 0, 0));
            var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, new BaseColor(128, 128, 128));

            // Header with Company Info and Logo
            var headerTable = new PdfPTable(2);
            headerTable.WidthPercentage = 100;
            headerTable.SetWidths(new float[] { 75, 25 });

            // Company Info Cell
            var companyCell = new PdfPCell();
            companyCell.Border = Rectangle.NO_BORDER;
            companyCell.PaddingBottom = 10;

            if (companyData != null)
            {
                companyCell.AddElement(new Paragraph(companyData.razaoSocial?.ToString() ?? "Empresa não identificada", headerFont));
                
                if (companyData.cnpj != null)
                    companyCell.AddElement(new Paragraph($"CNPJ: {companyData.cnpj}", bodyFont));
                
                if (companyData.logradouro != null)
                {
                    var address = $"{companyData.logradouro}";
                    if (companyData.numero != null) address += $", {companyData.numero}";
                    if (companyData.bairro != null) address += $" - {companyData.bairro}";
                    if (companyData.cidade != null) address += $", {companyData.cidade}";
                    if (companyData.cep != null) address += $" - CEP: {companyData.cep}";
                    companyCell.AddElement(new Paragraph(address, bodyFont));
                }
                
                if (companyData.email != null)
                    companyCell.AddElement(new Paragraph($"Email: {companyData.email}", bodyFont));
                if (companyData.telefone != null)
                    companyCell.AddElement(new Paragraph($"Telefone: {companyData.telefone}", bodyFont));
            }
            else
            {
                companyCell.AddElement(new Paragraph("Informações da empresa não disponíveis", bodyFont));
            }
            headerTable.AddCell(companyCell);

            // Logo Cell (placeholder for now)
            var logoCell = new PdfPCell(new Paragraph("LOGO\nEMPRESA", headerFont) { Alignment = Element.ALIGN_CENTER });
            logoCell.Border = Rectangle.BOX;
            logoCell.BorderColor = new BaseColor(192, 192, 192);
            logoCell.HorizontalAlignment = Element.ALIGN_CENTER;
            logoCell.VerticalAlignment = Element.ALIGN_MIDDLE;
            logoCell.MinimumHeight = 80;
            headerTable.AddCell(logoCell);

            document.Add(headerTable);
            document.Add(new Paragraph(new Chunk("\n")));

            // Report Title
            var title = new Paragraph("RELATÓRIO FINAL DE ELEIÇÃO", titleFont);
            title.Alignment = Element.ALIGN_CENTER;
            title.SpacingAfter = 10;
            document.Add(title);

            var electionTitle = new Paragraph($"Eleição: {election.Title}", subtitleFont);
            electionTitle.Alignment = Element.ALIGN_CENTER;
            electionTitle.SpacingAfter = 5;
            document.Add(electionTitle);

            var dateInfo = new Paragraph($"Gerado em: {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")}", bodyFont);
            dateInfo.Alignment = Element.ALIGN_CENTER;
            dateInfo.SpacingAfter = 20;
            document.Add(dateInfo);

            // Election Details
            var detailsTable = new PdfPTable(2);
            detailsTable.WidthPercentage = 100;
            detailsTable.SetWidths(new float[] { 30, 70 });
            
            AddDetailRow(detailsTable, "Data de Início:", election.StartDate.ToString("dd/MM/yyyy HH:mm"), headerFont, bodyFont);
            AddDetailRow(detailsTable, "Data de Término:", election.EndDate.ToString("dd/MM/yyyy HH:mm"), headerFont, bodyFont);
            AddDetailRow(detailsTable, "Tipo de Eleição:", election.ElectionType, headerFont, bodyFont);
            AddDetailRow(detailsTable, "Status:", "ENCERRADA", headerFont, bodyFont);
            
            document.Add(detailsTable);
            document.Add(new Paragraph(new Chunk("\n")));

            // Summary Section
            var summaryTitle = new Paragraph("RESUMO ESTATÍSTICO FINAL", headerFont);
            summaryTitle.SpacingAfter = 10;
            document.Add(summaryTitle);

            var summaryTable = new PdfPTable(2);
            summaryTable.WidthPercentage = 100;
            summaryTable.SetWidths(new float[] { 60, 40 });

            if (reportData != null)
            {
                AddSummaryRow(summaryTable, "Total de Votos Computados:", reportData.totalVotes?.ToString() ?? "0", headerFont, bodyFont);
                AddSummaryRow(summaryTable, "Total de Eleitores Aptos:", reportData.totalRegisteredVoters?.ToString() ?? "0", headerFont, bodyFont);
                AddSummaryRow(summaryTable, "Percentual de Participação:", reportData.participationPercentage?.ToString() ?? "0" + "%", headerFont, bodyFont);
                AddSummaryRow(summaryTable, "Total de Cargos:", reportData.totalPositions?.ToString() ?? "0", headerFont, bodyFont);
                AddSummaryRow(summaryTable, "Total de Candidatos:", reportData.totalCandidates?.ToString() ?? "0", headerFont, bodyFont);
                AddSummaryRow(summaryTable, "Votos Válidos:", reportData.validVotes?.ToString() ?? "0", headerFont, bodyFont);
                AddSummaryRow(summaryTable, "Votos em Branco:", reportData.blankVotes?.ToString() ?? "0", headerFont, bodyFont);
                AddSummaryRow(summaryTable, "Votos Nulos:", reportData.nullVotes?.ToString() ?? "0", headerFont, bodyFont);
            }
            else
            {
                AddSummaryRow(summaryTable, "Status:", "Dados não disponíveis", headerFont, bodyFont);
            }

            document.Add(summaryTable);
            document.Add(new Paragraph(new Chunk("\n")));

            // Positions and Results (if available)
            if (reportData?.positions != null)
            {
                var positionsTitle = new Paragraph("RESULTADOS POR CARGO", headerFont);
                positionsTitle.SpacingAfter = 10;
                document.Add(positionsTitle);

                foreach (var position in reportData.positions)
                {
                    document.Add(new Paragraph($"Cargo: {position.positionName}", headerFont));
                    
                    var positionTable = new PdfPTable(4);
                    positionTable.WidthPercentage = 100;
                    positionTable.SetWidths(new float[] { 50, 15, 15, 20 });
                    
                    // Header
                    var candidateHeader = new PdfPCell(new Phrase("Candidato", headerFont));
                    candidateHeader.BackgroundColor = new BaseColor(240, 240, 240);
                    candidateHeader.Padding = 5;
                    positionTable.AddCell(candidateHeader);

                    var numberHeader = new PdfPCell(new Phrase("Número", headerFont));
                    numberHeader.BackgroundColor = new BaseColor(240, 240, 240);
                    numberHeader.HorizontalAlignment = Element.ALIGN_CENTER;
                    numberHeader.Padding = 5;
                    positionTable.AddCell(numberHeader);

                    var votesHeader = new PdfPCell(new Phrase("Votos", headerFont));
                    votesHeader.BackgroundColor = new BaseColor(240, 240, 240);
                    votesHeader.HorizontalAlignment = Element.ALIGN_CENTER;
                    votesHeader.Padding = 5;
                    positionTable.AddCell(votesHeader);

                    var percentHeader = new PdfPCell(new Phrase("Percentual", headerFont));
                    percentHeader.BackgroundColor = new BaseColor(240, 240, 240);
                    percentHeader.HorizontalAlignment = Element.ALIGN_CENTER;
                    percentHeader.Padding = 5;
                    positionTable.AddCell(percentHeader);

                    // Candidates
                    foreach (var candidate in position.candidates)
                    {
                        positionTable.AddCell(new PdfPCell(new Phrase(candidate.candidateName?.ToString() ?? "N/A", bodyFont)) { Padding = 3 });
                        
                        var numberCell = new PdfPCell(new Phrase(candidate.candidateNumber?.ToString() ?? "N/A", bodyFont));
                        numberCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        numberCell.Padding = 3;
                        positionTable.AddCell(numberCell);
                        
                        var voteCell = new PdfPCell(new Phrase(candidate.voteCount?.ToString() ?? "0", bodyFont));
                        voteCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        voteCell.Padding = 3;
                        positionTable.AddCell(voteCell);

                        var percentCell = new PdfPCell(new Phrase(candidate.percentage?.ToString() ?? "0" + "%", bodyFont));
                        percentCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        percentCell.Padding = 3;
                        positionTable.AddCell(percentCell);
                    }
                    
                    document.Add(positionTable);
                    document.Add(new Paragraph(new Chunk("\n")));
                }
            }

            // Footer with seal hash
            document.Add(new Paragraph(new Chunk("\n\n")));
            var footer = new Paragraph("Este documento comprova os resultados finais da eleição.", smallFont);
            footer.Alignment = Element.ALIGN_CENTER;
            footer.SpacingBefore = 20;
            document.Add(footer);

            var sealInfo = new Paragraph($"Hash do Lacre: {sealHash}", smallFont);
            sealInfo.Alignment = Element.ALIGN_CENTER;
            document.Add(sealInfo);

            var disclaimer = new Paragraph("Documento gerado automaticamente pelo Sistema de Gestão Eleitoral.", smallFont);
            disclaimer.Alignment = Element.ALIGN_CENTER;
            document.Add(disclaimer);

            document.Close();
            return stream.ToArray();
        }
    }
}
