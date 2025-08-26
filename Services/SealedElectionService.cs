using System.Net.Http;
using System.Text.Json;
using System.Security.Claims;

namespace ElectionAdminPanel.Web.Services
{
    public interface ISealedElectionService
    {
        Task<bool> HasSealedElectionsAsync();
        Task<List<int>> GetSealedElectionIdsAsync();
        bool IsActionAllowed(string controllerName, string actionName);
        void ClearCache();
    }

    public class SealedElectionService : ISealedElectionService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static DateTime _lastCheck = DateTime.MinValue;
        private static bool _hasSealedElections = false;
        private static List<int> _sealedElectionIds = new List<int>();
        private static readonly TimeSpan CacheTimeout = TimeSpan.FromMinutes(1); // Cache por 1 minuto

        // Ações permitidas quando há eleições lacradas
        // REGRAS: Apenas VISUALIZAÇÃO e REDEFINIÇÃO DE SENHA de eleitores permitida
        private readonly Dictionary<string, HashSet<string>> _allowedActions = new()
        {
            {
                "voter", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    // PERMITIDO: Visualizar eleitores e editar email/telefone
                    "list", "edit", "details",
                    // PERMITIDO: Redefinir senhas de eleitores APENAS
                    "sendpasswordreset", "sendmasspasswordreset", "testemailconfiguration"
                    // BLOQUEADO: sendmassemail, sendindividualemail (envio geral de emails)
                    // BLOQUEADO: create, delete (criação/exclusão de eleitores)
                }
            },
            {
                "election", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    // PERMITIDO: Apenas visualizar eleições e status de lacre
                    "list", "getsealedelectionsstatus", "details"
                    // BLOQUEADO: create, edit, delete, updatestatus
                }
            },
            {
                "candidate", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    // PERMITIDO: Apenas visualizar candidatos
                    "list", "details"
                    // BLOQUEADO: create, edit, delete, upload-photo
                }
            },
            {
                "position", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    // PERMITIDO: Apenas visualizar cargos
                    "list", "details"
                    // BLOQUEADO: create, edit, delete
                }
            },
            {
                "report", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    // PERMITIDO: Todos os relatórios (são apenas visualização)
                    "index", "auditlogs", "statistics", "securityreport", "useractivity", 
                    "entityhistory", "suspiciousactivity", "export", "dashboard", "zeresima"
                }
            },
            {
                "home", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    // PERMITIDO: Páginas básicas
                    "index", "privacy", "dashboard"
                }
            },
            {
                "auth", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    // PERMITIDO: Autenticação básica
                    "login", "logout", "accessdenied"
                }
            },
            {
                "company", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    // PERMITIDO: Apenas visualizar empresas
                    "list", "details"
                    // BLOQUEADO: create, edit, delete, upload-logo
                }
            }
        };

        public SealedElectionService(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<bool> HasSealedElectionsAsync()
        {
            await RefreshCacheIfNeeded();
            return _hasSealedElections;
        }

        public async Task<List<int>> GetSealedElectionIdsAsync()
        {
            await RefreshCacheIfNeeded();
            return new List<int>(_sealedElectionIds);
        }

        public bool IsActionAllowed(string controllerName, string actionName)
        {
            if (!_hasSealedElections)
                return true;

            var controller = controllerName.ToLower();
            var action = actionName.ToLower();

            if (_allowedActions.ContainsKey(controller))
            {
                return _allowedActions[controller].Contains(action);
            }

            // Por padrão, bloquear ações não explicitamente permitidas
            return false;
        }

        public void ClearCache()
        {
            _lastCheck = DateTime.MinValue;
            _hasSealedElections = false;
            _sealedElectionIds = new List<int>();
            System.Console.WriteLine("[DEBUG] Sealed elections cache cleared");
        }

        private async Task RefreshCacheIfNeeded()
        {
            // Verificar se o cache ainda é válido
            if (DateTime.UtcNow - _lastCheck < CacheTimeout)
                return;

            try
            {
                var client = CreateAuthorizedClient();
                
                // Primeiro, buscar todas as eleições
                var response = await client.GetAsync("http://localhost:5110/api/election?page=1&limit=1000");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<ElectionListResponse>>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (apiResponse?.Success == true && apiResponse.Data?.Items != null)
                    {
                        var sealedIds = new List<int>();
                        var hasSealed = false;

                        // Verificar para cada eleição se está selada (selo de sistema OU status completed)
                        foreach (var item in apiResponse.Data.Items)
                        {
                            try
                            {
                                // Verificação 1: Status completed indica eleição automaticamente lacrada
                                if (string.Equals(item.Status, "completed", StringComparison.OrdinalIgnoreCase))
                                {
                                    sealedIds.Add(item.Id);
                                    hasSealed = true;
                                    continue;
                                }

                                // Verificação 2: Verificar se existe um selo de sistema explícito
                                var sealResponse = await client.GetAsync($"http://localhost:5110/api/SystemSeal/latest/{item.Id}");
                                if (sealResponse.IsSuccessStatusCode)
                                {
                                    var sealContent = await sealResponse.Content.ReadAsStringAsync();
                                    var sealApiResponse = JsonSerializer.Deserialize<ApiResponse<SystemSealData>>(sealContent, new JsonSerializerOptions
                                    {
                                        PropertyNameCaseInsensitive = true
                                    });
                                    
                                    // Se encontrou um selo válido para esta eleição
                                    if (sealApiResponse?.Success == true && sealApiResponse.Data != null && sealApiResponse.Data.IsValid)
                                    {
                                        sealedIds.Add(item.Id);
                                        hasSealed = true;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // Se houver erro ao verificar selo de uma eleição específica, continuar para a próxima
                                continue;
                            }
                        }

                        _sealedElectionIds = sealedIds;
                        _hasSealedElections = hasSealed;
                        _lastCheck = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception)
            {
                // Em caso de erro, assumir que não há eleições lacradas
                _hasSealedElections = false;
                _sealedElectionIds = new List<int>();
                _lastCheck = DateTime.UtcNow;
            }
        }

        private HttpClient CreateAuthorizedClient()
        {
            var client = _httpClientFactory.CreateClient();
            var httpContext = _httpContextAccessor.HttpContext;
            
            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                var token = httpContext.User.FindFirst("AccessToken")?.Value;
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
            }
            
            return client;
        }
    }

    // Classes de apoio para deserialização
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }

    public class ElectionListResponse
    {
        public List<ElectionItem> Items { get; set; } = new List<ElectionItem>();
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public int CurrentPage { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    public class ElectionItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class SystemSealData
    {
        public string SealHash { get; set; } = string.Empty;
        public string SealType { get; set; } = string.Empty;
        public int ElectionId { get; set; }
        public DateTime SealedAt { get; set; }
        public int SealedBy { get; set; }
        public string SystemData { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string? UserAgent { get; set; }
        public bool IsValid { get; set; }
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}