using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElectionAdminPanel.Web.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ElectionAdminPanel.Web.BackgroundServices
{
    public class ZeresimaReportService : BackgroundService
    {
        private readonly ILogger<ZeresimaReportService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public ZeresimaReportService(ILogger<ZeresimaReportService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Zeresima Report Service iniciado - Verificando a cada 60 segundos");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogDebug("🔍 Executando verificação de relatórios...");
                    
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var reportController = scope.ServiceProvider.GetRequiredService<ReportController>();
                        
                        // Executar ambas as verificações com token compartilhado
                        await reportController.priciGenerateZeresimaReport();
                        
                        // Aguardar 5 segundos antes da segunda verificação para evitar sobrecarga
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        
                        await reportController.GenerateFinalReport();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erro ao gerar relatórios");
                }

                // Verificar a cada 60 segundos (reduzindo frequência)
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }

            _logger.LogInformation("⏹️ Zeresima Report Service parando...");
        }
    }
}