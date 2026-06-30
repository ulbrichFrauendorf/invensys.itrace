namespace Invensys.ITrace.Application.Common.Interfaces;

public interface IDsnGenerator
{
    string Create(string applicationName, string environment);
    string Hash(string dsn);
}
