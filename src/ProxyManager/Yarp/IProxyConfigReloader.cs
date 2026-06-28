namespace West94.ProxyManager.Yarp;

/// <summary>Triggers a live reload of YARP routing configuration from the database.</summary>
public interface IProxyConfigReloader
{
    void Reload();
}
