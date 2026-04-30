# SKILL: Security, Authentication & Authorization Patterns

## Trigger
Activate when working on user management, login, roles, access control, API authentication, or any security-related code.

---

## 1. Identity Configuration

ASP.NET Core Identity is configured in `Program.cs` with these settings:

```csharp
builder.Services.AddDefaultIdentity<IdentityUser>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromDays(365 * 100); // 100 years
    options.Lockout.MaxFailedAccessAttempts = 5;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddErrorDescriber<ArabicIdentityErrorDescriber>();
```

### Key Points
- Email confirmation is **disabled** (`RequireConfirmedAccount = false`).
- Lockout duration is effectively permanent (100 years) — this is intentional for user deactivation.
- Error messages are localized to **Arabic** via `ArabicIdentityErrorDescriber` (in `Localization/`).

---

## 2. Global Authentication Requirement

All controllers require authentication by default via a global authorization filter:

```csharp
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});
```

**You do NOT need to add `[Authorize]` to individual controllers** — it's already enforced globally. However, controllers add `[Authorize(Roles = "...")]` when restricting to specific roles.

To allow anonymous access, use `[AllowAnonymous]` explicitly.

---

## 3. Role System

### Seeded Roles (from `Program.cs`)

```csharp
string[] roles = { "Admin", "Doctor", "LabTech", "Cashier", "Pharmacist" };
```

These are seeded on startup. The canonical list is also in `UsersController.SystemRoles`.

### Default Admin

Seeded automatically if no admin exists:
- Email: `admin@clinic.local`
- Password: `Admin@123!`

### Role-Based Authorization by Controller

| Controller | Roles |
|-----------|-------|
| `UsersController` | `Admin` only |
| `PharmacySalesController` | `Pharmacist, Admin` |
| `PharmacyPurchasesController` | `Pharmacist, Admin` |
| `PharmacyItemsController` | `Pharmacist, Admin` |
| `AppointmentsController.UpdateStatus` | `Cashier, Admin` |
| `AppointmentsController.RefundItem` | `Cashier, Admin` |
| `LaboratoryController` | `LabTech, Admin` |

---

## 4. User Deactivation (NOT Deletion)

Users are NEVER hard-deleted. Instead, use Identity Lockout:

```csharp
// Deactivate
await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

// Reactivate
await _userManager.SetLockoutEndDateAsync(user, null);

// Check if locked
bool isLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.Now;
```

**Self-lockout prevention:** A user cannot deactivate their own account:
```csharp
if (user.Id == _userManager.GetUserId(User))
{
    ShowAlert("لا يمكنك إيقاف تفعيل حسابك الشخصي.", "warning");
    return RedirectToAction(nameof(Index));
}
```

---

## 5. Cookie Configuration

```csharp
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});
```

The `AccessDeniedPath` points to a custom Arabic 403 page at `AccountController`.

---

## 6. Anti-Forgery Tokens

All POST forms MUST include `[ValidateAntiForgeryToken]`:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(MyViewModel model) { ... }
```

**Exception:** Lab device API endpoints use `[IgnoreAntiforgeryToken]` and authenticate via `X-Connector-Api-Key` header instead. This is the ONLY approved exception.

---

## 7. Lab Connector API Authentication

External lab hardware sends results via API. These endpoints:

1. Use `[IgnoreAntiforgeryToken]` — devices cannot produce CSRF tokens.
2. Authenticate via `X-Connector-Api-Key` header checked against `LabConnectorApiOptions`.
3. Configuration is in `appsettings.json` under the `"LabConnectorApi"` section.

```csharp
builder.Services.Configure<LabConnectorApiOptions>(
    builder.Configuration.GetSection(LabConnectorApiOptions.SectionName));
```

---

## 8. Arabic Error Messages

### Identity Errors
All Identity error messages are Arabic via `ArabicIdentityErrorDescriber` (in `Localization/ArabicIdentityErrorDescriber.cs`). Currently overrides:
- `PasswordRequiresUpper` → "يجب أن تحتوي كلمة المرور على حرف كبير..."
- `PasswordRequiresLower` → "يجب أن تحتوي كلمة المرور على حرف صغير..."
- `PasswordRequiresDigit` → "يجب أن تحتوي كلمة المرور على رقم..."
- `PasswordRequiresNonAlphanumeric` → "يجب أن تحتوي كلمة المرور على رمز..."
- `PasswordTooShort` → "يجب أن تتكون كلمة المرور من N أحرف..."

When adding new Identity error overrides, add them to this same class. Keep all messages in Arabic.

### DataAnnotation Errors
Model validation errors are defined inline in Arabic using `ErrorMessage`:
```csharp
[Required(ErrorMessage = "المورد مطلوب")]
```

---

## 9. User Management CRUD Pattern (`UsersController`)

- **Create:** Uses `_userManager.CreateAsync()` + `AddToRoleAsync()`. Returns JSON on success.
- **Edit Role:** Remove all current roles → add new role. Single-role-per-user design.
- **Reset Password:** `GeneratePasswordResetTokenAsync()` → `ResetPasswordAsync()`.
- **Toggle Status:** Lockout toggle (see §4 above).
- All modal dialogs rendered as PartialViews (`_CreateUserPartial`, `_EditUserPartial`, `_ResetPasswordPartial`).
