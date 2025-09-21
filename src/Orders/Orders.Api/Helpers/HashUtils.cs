using System.Security.Cryptography;
using System.Text;

namespace Orders.Api.Helpers
{
    public static class HashUtils
    {
        public static string ComputeSha256Hash(string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data ?? string.Empty);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
