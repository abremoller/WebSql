using System.Security.Cryptography;

namespace WebSql.Server.Security
{
    /// <summary>PBKDF2-SHA256 password hashing. Format: pbkdf2-sha256$iterations$saltBase64$hashBase64</summary>
    public static class PasswordHasher
    {
        private const string Prefix = "pbkdf2-sha256";
        private const int SaltBytes = 16;
        private const int HashBytes = 32;
        private const int DefaultIterations = 210_000;
        private const int MinIterations = 100_000;

        public static string Hash(string password, int iterations = DefaultIterations)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltBytes);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashBytes);
            return $"{Prefix}${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool IsWellFormed(string? stored) => TryParse(stored, out _, out _, out _);

        public static bool Verify(string password, string? stored)
        {
            if (!TryParse(stored, out var iterations, out var salt, out var expected))
                return false;

            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        private static bool TryParse(string? stored, out int iterations, out byte[] salt, out byte[] hash)
        {
            iterations = 0; salt = []; hash = [];
            if (string.IsNullOrWhiteSpace(stored)) return false;

            var parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != Prefix) return false;
            if (!int.TryParse(parts[1], out iterations) || iterations < MinIterations) return false;

            try
            {
                salt = Convert.FromBase64String(parts[2]);
                hash = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException) { return false; }

            return salt.Length >= 8 && hash.Length >= 16;
        }
    }
}
