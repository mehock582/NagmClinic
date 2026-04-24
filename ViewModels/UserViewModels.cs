using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NagmClinic.ViewModels
{
    public class UserIndexViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty; // In this setup Email is usually the identifier
        public string Role { get; set; } = "بدون دور";
        public bool IsLocked { get; set; }
    }

    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "بريد إلكتروني غير صالح")]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "تأكيد كلمة المرور")]
        [Compare("Password", ErrorMessage = "كلمة المرور وتأكيدها غير متطابقتين")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "يجب اختيار دور للمستخدم")]
        [Display(Name = "الدور")]
        public string SelectedRole { get; set; } = string.Empty;
    }

    public class EditUserRoleViewModel
    {
        public string UserId { get; set; } = string.Empty;
        
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "يجب اختيار دور للمستخدم")]
        [Display(Name = "الدور")]
        public string SelectedRole { get; set; } = string.Empty;
    }

    public class ResetPasswordViewModel
    {
        public string UserId { get; set; } = string.Empty;

        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
        [StringLength(100, ErrorMessage = "يجب أن تكون {0} على الأقل {2} أحرف.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور الجديدة")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "تأكيد كلمة المرور")]
        [Compare("NewPassword", ErrorMessage = "كلمة المرور وتأكيدها غير متطابقتين")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
