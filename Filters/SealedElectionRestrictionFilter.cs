using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ElectionAdminPanel.Web.Services;

namespace ElectionAdminPanel.Web.Filters
{
    public class SealedElectionRestrictionFilter : IAsyncActionFilter
    {
        private readonly ISealedElectionService _sealedElectionService;

        public SealedElectionRestrictionFilter(ISealedElectionService sealedElectionService)
        {
            _sealedElectionService = sealedElectionService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var controllerName = context.RouteData.Values["controller"]?.ToString() ?? "";
            var actionName = context.RouteData.Values["action"]?.ToString() ?? "";

            // Verificar se há eleições lacradas
            var hasSealedElections = await _sealedElectionService.HasSealedElectionsAsync();

            if (hasSealedElections)
            {
                // Verificar se a ação é permitida
                var isAllowed = _sealedElectionService.IsActionAllowed(controllerName, actionName);

                if (!isAllowed)
                {
                    // Se for uma requisição AJAX/JSON, retornar erro JSON
                    if (context.HttpContext.Request.Headers.Accept.ToString().Contains("application/json") ||
                        context.HttpContext.Request.ContentType?.Contains("application/json") == true)
                    {
                        context.Result = new JsonResult(new
                        {
                            success = false,
                            message = "Esta ação não é permitida quando há eleições lacradas. Apenas consultas e gerenciamento de senhas de eleitores são permitidos.",
                            blocked = true,
                            reason = "sealed_election"
                        })
                        {
                            StatusCode = 403
                        };
                        return;
                    }

                    // Para requisições normais, redirecionar com mensagem de erro
                    if (context.Controller is Controller controller)
                    {
                        controller.TempData["ErrorMessage"] = 
                            "Esta ação não é permitida quando há eleições lacradas. Apenas consultas e gerenciamento de senhas de eleitores são permitidos.";
                        controller.TempData["SealedElectionRestriction"] = true;
                    }

                    // Redirecionar para uma página apropriada baseada no controller
                    var redirectAction = GetRedirectAction(controllerName);
                    context.Result = new RedirectToActionResult(redirectAction.Action, redirectAction.Controller, null);
                    return;
                }
            }

            // Adicionar informação sobre eleições lacradas ao ViewBag para uso nas views
            if (context.Controller is Controller mvcController)
            {
                mvcController.ViewBag.HasSealedElections = hasSealedElections;
                if (hasSealedElections)
                {
                    mvcController.ViewBag.SealedElectionIds = await _sealedElectionService.GetSealedElectionIdsAsync();
                }
            }

            await next();
        }

        private (string Action, string Controller) GetRedirectAction(string controllerName)
        {
            return controllerName.ToLower() switch
            {
                "election" => ("List", "Election"),
                "candidate" => ("List", "Candidate"),
                "position" => ("List", "Position"),
                "voter" => ("List", "Voter"),
                "report" => ("Index", "Report"),
                _ => ("Index", "Home")
            };
        }
    }

    // Attribute para aplicar facilmente o filtro
    public class RestrictWhenSealedAttribute : ServiceFilterAttribute
    {
        public RestrictWhenSealedAttribute() : base(typeof(SealedElectionRestrictionFilter))
        {
        }
    }
}