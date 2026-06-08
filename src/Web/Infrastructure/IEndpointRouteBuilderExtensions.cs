namespace Invensys.ITrace.Web.Infrastructure;

public static class IEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapGet(this IEndpointRouteBuilder builder, Delegate handler, string pattern = "")
    {
        EnsureNamedHandler(handler);
        builder.MapGet(pattern, handler).WithName(handler.Method.Name);

        return builder;
    }

    public static IEndpointRouteBuilder MapPost(this IEndpointRouteBuilder builder, Delegate handler, string pattern = "")
    {
        EnsureNamedHandler(handler);
        builder.MapPost(pattern, handler).WithName(handler.Method.Name);

        return builder;
    }

    public static IEndpointRouteBuilder MapPut(this IEndpointRouteBuilder builder, Delegate handler, string pattern)
    {
        EnsureNamedHandler(handler);
        builder.MapPut(pattern, handler).WithName(handler.Method.Name);

        return builder;
    }

    public static IEndpointRouteBuilder MapDelete(this IEndpointRouteBuilder builder, Delegate handler, string pattern)
    {
        EnsureNamedHandler(handler);
        builder.MapDelete(pattern, handler).WithName(handler.Method.Name);

        return builder;
    }

    private static void EnsureNamedHandler(Delegate handler)
    {
        if (handler.Method.Name.StartsWith("<", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Endpoint handlers must be named methods.");
        }
    }
}
