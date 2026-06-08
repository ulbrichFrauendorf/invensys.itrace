namespace Invensys.ITrace.Web.Infrastructure;

public abstract class EndpointGroupBase
{
    public virtual string GroupName => GetType().Name;

    public abstract void Map(WebApplication app);
}
