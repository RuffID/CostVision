using CostVision.Application.Abstractions.Service.Authorize;
using CostVision.Infrastructure.Models.Constants;
using System.Security.Cryptography;

namespace CostVision.Infrastructure.Services.Helpers
{
    public class Hasher : IPasswordHasher
    {
        public string Hash(string input)
        {
            byte[] salt = GenerateSalt();
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                input,
                salt,
                HashSettingsConstants.ITERATIONS,
                HashAlgorithmName,
                HashSettingsConstants.KEY_SIZE
            );

            return string.Join(
                HashSettingsConstants.SEPARATOR,
                Convert.ToHexString(hash),
                Convert.ToHexString(salt),
                HashSettingsConstants.ITERATIONS,
                HashAlgorithmName
            );
        }

        public bool Verify(string input, string hashString)
        {
            string[] segments = hashString.Split(HashSettingsConstants.SEPARATOR);
            byte[] hash = Convert.FromHexString(segments[0]);
            byte[] salt = Convert.FromHexString(segments[1]);
            int iterations = int.Parse(segments[2]);
            HashAlgorithmName algorithm = new(segments[3]);
            byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(
                input,
                salt,
                iterations,
                algorithm,
                hash.Length
            );
            return CryptographicOperations.FixedTimeEquals(inputHash, hash);
        }

        private byte[] GenerateSalt() { return RandomNumberGenerator.GetBytes(HashSettingsConstants.SALT_SIZE); }

        private HashAlgorithmName HashAlgorithmName { get { return new HashAlgorithmName(HashSettingsConstants.ALGORITHM); } }
    }
}
