using System;
using System.Security.Cryptography;
using System.Text;

namespace ShooterMover.Application.Modifiers.StatusEffects
{
    internal static class StatusEffectLocalHashV1
    {
        internal static string Hash(string value)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(
                    System.Text.Encoding.UTF8.GetBytes(
                        value ?? string.Empty));
                return BitConverter.ToString(bytes)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}
