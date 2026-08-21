using System.Security.Cryptography;

namespace Intranet.Services
{
    public class PasswordService
    {
        public string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);

            var hash = Rfc2898DeriveBytes.Pbkdf2(
              password,
              salt,
              100000,
              HashAlgorithmName.SHA256,
              32);

            return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
        }

        public bool Verify(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash))
                return false;

            var parts = storedHash.Split(':');
            if (parts.Length != 2)
                return false;

            var salt = Convert.FromBase64String(parts[0]);
            var stored = Convert.FromBase64String(parts[1]);

            var computed = Rfc2898DeriveBytes.Pbkdf2(
              password,
              salt,
              100000,
              HashAlgorithmName.SHA256,
              32);

            return CryptographicOperations.FixedTimeEquals(stored, computed);
        }
    }
}