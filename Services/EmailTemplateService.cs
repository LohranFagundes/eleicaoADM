using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ElectionAdminPanel.Web.Services
{
    public interface IEmailTemplateService
    {
        string GetPasswordResetTemplate(string recipientName, string resetToken, string resetUrl);
        string GetBaseUrl();
        string GetVotingSystemUrl();
    }

    public class EmailTemplateService : IEmailTemplateService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public EmailTemplateService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        public string GetBaseUrl()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var scheme = httpContext.Request.Scheme;
                var host = httpContext.Request.Host.Value;
                return $"{scheme}://{host}";
            }

            // Fallback para configuração
            return _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5115";
        }

        public string GetVotingSystemUrl()
        {
            return _configuration["AppSettings:VotingSystemUrl"] ?? "http://localhost:5112";
        }

        public string GetPasswordResetTemplate(string recipientName, string resetToken, string resetUrl)
        {
            var resetLink = $"{resetUrl}?token={resetToken}";
            
            return $@"
<!DOCTYPE html>
<html lang='pt-BR'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Redefinição de Senha</title>
    <style>
        body {{ 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            line-height: 1.6;
            color: #333;
            margin: 0;
            padding: 0;
            background-color: #f5f5f5;
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            overflow: hidden;
        }}
        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 24px;
            font-weight: 600;
        }}
        .header .icon {{
            font-size: 48px;
            margin-bottom: 10px;
            display: block;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .greeting {{
            font-size: 18px;
            margin-bottom: 20px;
            color: #2c3e50;
        }}
        .message {{
            font-size: 16px;
            margin-bottom: 30px;
            color: #555;
            line-height: 1.7;
        }}
        .reset-button {{
            display: inline-block;
            background: linear-gradient(135deg, #4CAF50 0%, #45a049 100%);
            color: white !important;
            padding: 18px 40px;
            text-decoration: none !important;
            border-radius: 8px;
            font-weight: 700;
            font-size: 18px;
            text-align: center;
            margin: 25px 0;
            border: none;
            box-shadow: 0 4px 15px rgba(76, 175, 80, 0.3);
            transition: all 0.3s ease;
            min-width: 250px;
        }}
        .reset-button:hover {{
            background: linear-gradient(135deg, #45a049 0%, #4CAF50 100%);
            transform: translateY(-3px);
            box-shadow: 0 6px 20px rgba(76, 175, 80, 0.4);
        }}
        .button-container {{
            text-align: center;
            margin: 30px 0;
            padding: 20px;
        }}
        .security-info {{
            background-color: #f8f9fa;
            border-left: 4px solid #007bff;
            padding: 15px;
            margin: 25px 0;
            border-radius: 4px;
        }}
        .security-info h3 {{
            margin-top: 0;
            color: #007bff;
            font-size: 16px;
        }}
        .security-info p {{
            margin-bottom: 0;
            font-size: 14px;
            color: #666;
        }}
        .warning {{
            background-color: #fff3cd;
            border: 1px solid #ffeaa7;
            color: #856404;
            padding: 15px;
            border-radius: 4px;
            margin: 20px 0;
            font-size: 14px;
        }}
        .footer {{
            background-color: #f8f9fa;
            padding: 25px 30px;
            text-align: center;
            border-top: 1px solid #dee2e6;
        }}
        .footer p {{
            margin: 0;
            font-size: 12px;
            color: #6c757d;
        }}
        .token-info {{
            background-color: #e3f2fd;
            border: 1px solid #bbdefb;
            padding: 10px;
            border-radius: 4px;
            margin: 15px 0;
            font-family: monospace;
            font-size: 12px;
            color: #1565c0;
            word-break: break-all;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <span class='icon'>🔐</span>
            <h1>Redefinição de Senha</h1>
        </div>
        
        <div class='content'>
            <div class='greeting'>Olá, {recipientName}!</div>
            
            <div class='message'>
                Você solicitou a redefinição de sua senha no Sistema de Eleições. 
                Para criar uma nova senha, clique no botão abaixo:
            </div>
            
            <div class='button-container'>
                <a href='{resetLink}' class='reset-button'>
                    🔐 REDEFINIR MINHA SENHA
                </a>
            </div>
            
            <div style='text-align: center; margin: 20px 0; font-size: 14px; color: #666;'>
                <p><strong>Não consegue clicar no botão?</strong><br>
                Copie e cole este link no seu navegador:<br>
                <span style='color: #4CAF50; font-family: monospace; word-break: break-all;'>{resetLink}</span></p>
            </div>
            
            <div class='security-info'>
                <h3>📋 Informações de Segurança</h3>
                <p><strong>✅ Este link é válido por 30 minutos</strong><br>
                ⏰ Após este período, será necessário solicitar uma nova redefinição<br>
                🔒 Por segurança, este link só pode ser usado uma única vez</p>
            </div>
            
            <div class='warning'>
                <strong>⚠️ Não solicitou esta redefinição?</strong><br>
                Se você não solicitou a redefinição de senha, pode ignorar este email com segurança. 
                Sua conta permanecerá protegida e nenhuma alteração será feita.
            </div>
            
            <div class='token-info'>
                <strong>Token de Verificação:</strong> {resetToken}<br>
                <small>Use este token se o link não funcionar corretamente</small>
            </div>
            
            <div class='message' style='margin-top: 30px; font-size: 14px; color: #666;'>
                <strong>Precisa de ajuda?</strong><br>
                Se você tiver dificuldades para redefinir sua senha, entre em contato com o suporte 
                do sistema ou com o administrador responsável.
            </div>
        </div>
        
        <div class='footer'>
            <p>Este email foi gerado automaticamente pelo Sistema de Eleições.<br>
            Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm:ss} (Horário de Brasília)<br>
            <strong>Por favor, não responda este email.</strong></p>
        </div>
    </div>
</body>
</html>";
        }
    }
}