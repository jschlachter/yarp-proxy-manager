namespace West94.ProxyManager.Files.Assets;

public sealed class FileAssetNotFoundException(Guid id) : Exception($"No file asset with id '{id}' was found.")
{
    public Guid Id { get; } = id;
}
