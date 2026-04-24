using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NagmClinic.ViewModels;

namespace NagmClinic.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : BaseController
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public static readonly string[] SystemRoles = new[]
        {
            "Admin", "Doctor", "LabTech", "Cashier", "Pharmacist"
        };

        public UsersController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET: /Users
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetUsersData()
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault();

                int pageSize = length != null ? Convert.ToInt32(length) : 10;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                var usersQuery = _userManager.Users.AsQueryable();

                if (!string.IsNullOrEmpty(searchValue))
                {
                    usersQuery = usersQuery.Where(m => m.Email.Contains(searchValue));
                }

                int recordsTotal = await _userManager.Users.CountAsync();
                int recordsFiltered = await usersQuery.CountAsync();

                var users = await usersQuery.Skip(skip).Take(pageSize).ToListAsync();

                var data = new List<object>();
                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    data.Add(new
                    {
                        id = user.Id,
                        email = user.Email ?? "-",
                        role = roles.FirstOrDefault() ?? "بدون دور",
                        isLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.Now
                    });
                }

                return Json(new { draw = draw, recordsTotal = recordsTotal, recordsFiltered = recordsFiltered, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // GET: /Users/Create
        public IActionResult Create()
        {
            ViewBag.Roles = new SelectList(SystemRoles);
            return PartialView("_CreateUserPartial");
        }

        // POST: /Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = new SelectList(SystemRoles);
                return PartialView("_CreateUserPartial", model);
            }

            var user = new IdentityUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.SelectedRole);
                return Json(new { success = true, message = $"تم إنشاء المستخدم {model.Email} بنجاح." });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            ViewBag.Roles = new SelectList(SystemRoles);
            return PartialView("_CreateUserPartial", model);
        }

        // GET: /Users/Edit/id
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var model = new EditUserRoleViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? "-",
                SelectedRole = roles.FirstOrDefault() ?? ""
            };

            ViewBag.Roles = new SelectList(SystemRoles);
            return PartialView("_EditUserPartial", model);
        }

        // POST: /Users/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserRoleViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = new SelectList(SystemRoles);
                return PartialView("_EditUserPartial", model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, model.SelectedRole);

            return Json(new { success = true, message = $"تم تحديث صلاحيات المستخدم {user.Email} بنجاح." });
        }

        // GET: /Users/ResetPassword/id
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var model = new ResetPasswordViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? "-"
            };

            return PartialView("_ResetPasswordPartial", model);
        }

        // POST: /Users/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return PartialView("_ResetPasswordPartial", model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                return Json(new { success = true, message = $"تم إعادة تعيين كلمة مرور المستخدم {user.Email} بنجاح." });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return PartialView("_ResetPasswordPartial", model);
        }

        // POST: /Users/ToggleStatus/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (user.Id == _userManager.GetUserId(User))
            {
                ShowAlert("لا يمكنك إيقاف تفعيل حسابك الشخصي.", "warning");
                return RedirectToAction(nameof(Index));
            }

            bool isLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.Now;

            if (isLocked)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                ShowAlert($"تم تفعيل حساب {user.Email} بنجاح.");
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                ShowAlert($"تم إيقاف حساب {user.Email} بنجاح.", "warning");
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
