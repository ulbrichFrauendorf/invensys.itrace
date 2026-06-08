using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;

namespace Invensys.ITrace.Api.Services;

public sealed partial class DsnGenerator : IDsnGenerator
{
    public string Create(string applicationName, string environment, string siteName)
    {
        var key = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
        var slug = Slugify($"{applicationName}-{environment}-{siteName}");
        return $"itrace://{key}@collector/{slug}";
    }

    public string Hash(string dsn)
    {
        var bytes = Encoding.UTF8.GetBytes(dsn.Trim());
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string Slugify(string value)
    {
        var slug = NonSlugCharacter().Replace(value.Trim().ToLowerInvariant(), "-");
        return slug.Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacter();
}
