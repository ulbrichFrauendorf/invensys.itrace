using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace Invensys.ITrace.Web.Swagger;

public sealed class TagByGroupOperationProcessor : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        var operation = context.OperationDescription.Operation;
        var path = context.OperationDescription.Path?.Trim('/') ?? string.Empty;

        if (!string.IsNullOrEmpty(path))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length > 1 && string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase))
            {
                var group = segments[1];

                if (!operation.Tags.Contains(group))
                {
                    operation.Tags.Add(group);
                }
            }
        }

        var methodName = context.MethodInfo?.Name;
        if (string.IsNullOrWhiteSpace(operation.Summary) && !string.IsNullOrWhiteSpace(methodName))
        {
            operation.Summary = methodName;
        }

        return true;
    }
}
