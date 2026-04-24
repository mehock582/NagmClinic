using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace NagmClinic.Controllers
{
    public class AccountController : Controller
    {
        // GET: /Account/AccessDenied
        [AllowAnonymous]
        public IActionResult AccessDenied() => View();
    }
}
