using Microsoft.AspNetCore.Identity;

namespace NagmClinic.Localization
{
    public class ArabicIdentityErrorDescriber : IdentityErrorDescriber
    {
        public override IdentityError PasswordRequiresUpper()
        {
            return new IdentityError
            {
                Code = nameof(PasswordRequiresUpper),
                Description = "يجب أن تحتوي كلمة المرور على حرف كبير واحد على الأقل ('A'-'Z')."
            };
        }

        public override IdentityError PasswordRequiresLower()
        {
            return new IdentityError
            {
                Code = nameof(PasswordRequiresLower),
                Description = "يجب أن تحتوي كلمة المرور على حرف صغير واحد على الأقل ('a'-'z')."
            };
        }

        public override IdentityError PasswordRequiresDigit()
        {
            return new IdentityError
            {
                Code = nameof(PasswordRequiresDigit),
                Description = "يجب أن تحتوي كلمة المرور على رقم واحد على الأقل ('0'-'9')."
            };
        }

        public override IdentityError PasswordRequiresNonAlphanumeric()
        {
            return new IdentityError
            {
                Code = nameof(PasswordRequiresNonAlphanumeric),
                Description = "يجب أن تحتوي كلمة المرور على رمز واحد على الأقل."
            };
        }

        public override IdentityError PasswordTooShort(int length)
        {
            return new IdentityError
            {
                Code = nameof(PasswordTooShort),
                Description = $"يجب أن تتكون كلمة المرور من {length} أحرف على الأقل."
            };
        }
    }
}
