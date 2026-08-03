using System;
using System.Security.Cryptography;
using System.Text;

namespace ShooterMover.UI.Game
{
    internal static class RunFingerprint
    {
        public static string Hash(string material)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] digest = algorithm.ComputeHash(
                    Encoding.UTF8.GetBytes(material ?? string.Empty));
                return BitConverter.ToString(digest)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}
