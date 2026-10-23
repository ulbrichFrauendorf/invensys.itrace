namespace Invensys.ITrace.Domain.Configuration;

public class AngularSettings
{
    public required string Authority { get; set; }
    public required string ClientId { get; set; }
    public required string Scope { get; set; }
    public string? SpaClientUrl { get; set; }
}
