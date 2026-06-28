extern alias ProxyManagerApp;
using ProxyManagerApp::West94.ProxyManager.Handlers;
using ProxyManagerApp::West94.ProxyManager.Yarp;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

[Trait("Category", "Unit")]
public class ProxyHostChangedHandlerTests
{
    private sealed class SpyReloader : IProxyConfigReloader
    {
        public int ReloadCount { get; private set; }
        public void Reload() => ReloadCount++;
    }

    [Fact]
    public void Handle_ProxyHostCreatedEvent_CallsReload()
    {
        var spy = new SpyReloader();
        var handler = new ProxyHostChangedHandler(spy);

        handler.Handle(new ProxyHostCreatedEvent(
            Guid.NewGuid(), ["app.example.com"], "http://backend:8080", true, DateTimeOffset.UtcNow));

        Assert.Equal(1, spy.ReloadCount);
    }

    [Fact]
    public void Handle_ProxyHostUpdatedEvent_CallsReload()
    {
        var spy = new SpyReloader();
        var handler = new ProxyHostChangedHandler(spy);

        handler.Handle(new ProxyHostUpdatedEvent(
            Guid.NewGuid(), ["app.example.com"], "http://backend:8080", false, DateTimeOffset.UtcNow));

        Assert.Equal(1, spy.ReloadCount);
    }

    [Fact]
    public void Handle_ProxyHostDeletedEvent_CallsReload()
    {
        var spy = new SpyReloader();
        var handler = new ProxyHostChangedHandler(spy);

        handler.Handle(new ProxyHostDeletedEvent(
            Guid.NewGuid(), ["app.example.com"], DateTimeOffset.UtcNow));

        Assert.Equal(1, spy.ReloadCount);
    }

    [Fact]
    public void Handle_MultipleEvents_CallsReloadForEach()
    {
        var spy = new SpyReloader();
        var handler = new ProxyHostChangedHandler(spy);
        var id = Guid.NewGuid();

        handler.Handle(new ProxyHostCreatedEvent(id, ["a.example.com"], "http://a:8080", true, DateTimeOffset.UtcNow));
        handler.Handle(new ProxyHostUpdatedEvent(id, ["a.example.com"], "http://a:8080", false, DateTimeOffset.UtcNow));
        handler.Handle(new ProxyHostDeletedEvent(id, ["a.example.com"], DateTimeOffset.UtcNow));

        Assert.Equal(3, spy.ReloadCount);
    }
}
