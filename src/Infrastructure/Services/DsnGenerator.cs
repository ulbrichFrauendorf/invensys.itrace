using System.Security.Cryptography;
using System.Text;
using Invensys.ITrace.Application.Common.Interfaces;

namespace Invensys.ITrace.Infrastructure.Services;

public sealed class DsnGenerator : IDsnGenerator
{
    public string Create(string applicationName, string environment)
    {
        var entropy = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var slug = Slugify($"{applicationName}-{environment}");
        return $"itrace://{slug}-{entropy}";
    }

    public string Hash(string dsn)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(dsn));
        return Convert.ToHexString(bytes);
    }

    private static string Slugify(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();

        return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
