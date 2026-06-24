using System.Diagnostics;

namespace Invensys.ITrace.Client;

public static class ITraceDiagnostics
{
    public const string ActivitySourceName = "Invensys.ITrace.Client";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
