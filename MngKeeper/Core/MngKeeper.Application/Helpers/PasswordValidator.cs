namespace MngKeeper.Application.Helpers
{
    public static class PasswordValidator
    {
        /// <summary>
        /// Validates password strength
        /// </summary>
        /// <param name="password">Password to validate</param>
        /// <returns>Validation result with error message if invalid</returns>
        public static (bool IsValid, string? ErrorMessage) ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, "Password cannot be empty");
            }

            if (password.Length < 8)
            {
                return (false, "Password must be at least 8 characters long");
            }

            if (password.Length > 128)
            {
                return (false, "Password must be at most 128 characters long");
            }

            // Check for at least one uppercase letter
            if (!password.Any(char.IsUpper))
            {
                return (false, "Password must contain at least one uppercase letter");
            }

            // Check for at least one lowercase letter
            if (!password.Any(char.IsLower))
            {
                return (false, "Password must contain at least one lowercase letter");
            }

            // Check for at least one digit
            if (!password.Any(char.IsDigit))
            {
                return (false, "Password must contain at least one digit");
            }

            // Check for at least one special character
            if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            {
                return (false, "Password must contain at least one special character");
            }

            return (true, null);
        }
    }
}

