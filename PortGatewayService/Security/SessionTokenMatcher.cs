using System;
using System.Security.Cryptography;
using System.Text;

namespace PortTraining.PortGatewayService.Security
{
    // V1.1 section 10.1: "Gateway把令牌SHA-256与学生端预注册摘要进行常量时间比较". Uses
    // CryptographicOperations.FixedTimeEquals on the raw hash bytes rather than string
    // equality, which is not constant-time and would leak timing information about how
    // many leading bytes matched.
    public static class SessionTokenMatcher
    {
        // storedHashHex is expected in the "sha256:<64 lowercase hex chars>" form used
        // throughout V1.1 (section 4). Returns false (never throws) for any malformed
        // input instead of letting a format error leak through as a crash.
        public static bool Matches(string rawToken, string storedHashHex)
        {
            if (string.IsNullOrEmpty(rawToken) || string.IsNullOrEmpty(storedHashHex))
            {
                return false;
            }

            const string prefix = "sha256:";
            if (!storedHashHex.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            byte[] storedBytes;
            try
            {
                storedBytes = Convert.FromHexString(storedHashHex.Substring(prefix.Length));
            }
            catch (FormatException)
            {
                return false;
            }

            using (var sha256 = SHA256.Create())
            {
                var computedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawToken));
                if (computedBytes.Length != storedBytes.Length)
                {
                    return false;
                }

                return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
            }
        }
    }
}
