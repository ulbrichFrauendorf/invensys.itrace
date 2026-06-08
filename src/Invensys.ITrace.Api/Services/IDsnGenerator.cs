namespace Invensys.ITrace.Api.Services;

public interface IDsnGenerator
{
    string Create(string applicationName, string environment, string siteName);

    string Hash(string dsn);
}
