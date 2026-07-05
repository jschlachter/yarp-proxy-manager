using System.Runtime.CompilerServices;
using DotNet.Testcontainers.Configurations;

namespace West94.ProxyManager.API.Tests;

internal static class TestContainersConfig
{
    // Ryuk (the resource reaper) cannot mount the Docker socket in OrbStack/VM-based Docker environments.
    // Disabling it here is safe because xUnit's IAsyncLifetime ensures containers are stopped in DisposeAsync.
    [ModuleInitializer]
    internal static void Initialize()
    {
        TestcontainersSettings.ResourceReaperEnabled = false;
        TestcontainersSettings.DockerHostOverride = "unix:///var/run/docker.sock";
    }
}
