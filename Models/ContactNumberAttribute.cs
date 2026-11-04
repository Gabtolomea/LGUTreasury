using System.ComponentModel.DataAnnotations;

namespace LGUTreasury.Models
{
    /// <summary>
    /// Validates that a contact number contains numbers only. Letters are
    /// rejected outright so they can never be encoded into a stored number.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class ContactNumberAttribute : ValidationAttribute
    {
        public ContactNumberAttribute()
            : base(ContactNumberRules.LettersErrorMessage)
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var raw = value as string;
            if (string.IsNullOrWhiteSpace(raw)) return ValidationResult.Success;

            if (raw.Length > ContactNumberRules.MaxLength)
                return Fail(ContactNumberRules.LengthErrorMessage, validationContext);

            if (raw.Any(c => !ContactNumberRules.IsAllowedChar(c)))
                return Fail(ContactNumberRules.LettersErrorMessage, validationContext);

            var digits = ContactNumberRules.DigitCount(raw);
            if (digits < ContactNumberRules.MinDigits || digits > ContactNumberRules.MaxDigits)
                return Fail(ContactNumberRules.LengthErrorMessage, validationContext);

            return ValidationResult.Success;
        }

        private ValidationResult Fail(string message, ValidationContext validationContext)
        {
            var member = validationContext.MemberName ?? string.Empty;
            return new ValidationResult(message, new[] { member });
        }
    }
}
