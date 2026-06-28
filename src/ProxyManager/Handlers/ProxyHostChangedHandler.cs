using West94.ProxyManager.Core.Messages.Events;
using West94.ProxyManager.Yarp;

namespace West94.ProxyManager.Handlers;

/// <summary>
/// Wolverine message handler that triggers a YARP config reload whenever a ProxyHost is
/// created, updated, or deleted in ProxyManager.API.
/// </summary>
public sealed class ProxyHostChangedHandler(IProxyConfigReloader reloader)
{
    public void Handle(ProxyHostCreatedEvent _) => reloader.Reload();
    public void Handle(ProxyHostUpdatedEvent _) => reloader.Reload();
    public void Handle(ProxyHostDeletedEvent _) => reloader.Reload();
}
