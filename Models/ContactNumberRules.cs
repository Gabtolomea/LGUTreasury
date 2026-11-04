using System.Linq;

namespace LGUTreasury.Models
{
    /// <summary>
    /// Shared rules for contact numbers. A contact number is numeric only:
    /// letters (and any other alphabetic character) are never accepted, never
    /// stored and never displayed, so a number can never end up encoded with
    /// letters mixed in.
    /// </summary>
    public static class ContactNumberRules
    {
        public const string LettersErrorMessage = "Contact number must contain numbers only.";
        public const string LengthErrorMessage = "Contact number must be between 7 and 15 digits.";

        public const int MinDigits = 7;
        public const int MaxDigits = 15;
        public const int MaxLength = 20;

        /// <summary>Characters a user may type: digits plus common separators.</summary>
        public static bool IsAllowedChar(char c)
        {
            return (c >= '0' && c <= '9')
                || c == '+' || c == '-' || c == '(' || c == ')'
                || char.IsWhiteSpace(c);
        }

        /// <summary>Number of digits in a raw contact number.</summary>
        public static int DigitCount(string? value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            return value.Count(c => c >= '0' && c <= '9');
        }

        /// <summary>True when the raw value contains at least one letter.</summary>
        public static bool HasLetters(string? value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.Any(char.IsLetter);
        }

        /// <summary>
        /// True when the value is an acceptable contact number: no letters, only
        /// digits/separators, and a sane number of digits. Empty is allowed
        /// because the field is optional.
        /// </summary>
        public static bool IsValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;

            if (value.Length > MaxLength) return false;
            if (value.Any(c => !IsAllowedChar(c))) return false;

            var digits = DigitCount(value);
            return digits >= MinDigits && digits <= MaxDigits;
        }

        /// <summary>
        /// Canonical form used for storage: digits only, no letters, no separators.
        /// Returns null when the value holds no digits at all.
        /// </summary>
        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var digits = new string(value.Where(c => c >= '0' && c <= '9').ToArray());
            return digits.Length == 0 ? null : digits;
        }

        /// <summary>
        /// Display form of a stored contact number. Local mobile numbers are
        /// rendered as 09XX-XXX-XXXX; anything else falls back to plain digits.
        /// </summary>
        public static string? Format(string? value)
        {
            var digits = Normalize(value);
            if (digits == null) return null;

            if (digits.Length == 11 && digits.StartsWith("09"))
                return $"{digits.Substring(0, 4)}-{digits.Substring(4, 3)}-{digits.Substring(7)}";

            return digits;
        }
    }
}
