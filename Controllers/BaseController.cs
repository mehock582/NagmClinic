using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NagmClinic.Exceptions;

namespace NagmClinic.Controllers
{
    public class BaseController : Controller
    {
        protected void ShowAlert(string message, string type = "success")
        {
            TempData["AlertMessage"] = message;
            TempData["AlertType"] = type;
        }

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Exception is ConcurrencyException concurrencyEx)
            {
                ShowAlert(concurrencyEx.Message, "error");
                context.Result = RedirectToAction("Index");
                context.ExceptionHandled = true;
            }
            base.OnActionExecuted(context);
        }
    }
}
