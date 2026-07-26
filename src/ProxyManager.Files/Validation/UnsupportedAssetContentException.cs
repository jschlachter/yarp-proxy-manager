namespace West94.ProxyManager.Files.Validation;

/// <summary>Extension not allowlisted for the asset type, or magic bytes don't match a recognized format. Maps to 415.</summary>
public sealed class UnsupportedAssetContentException(string message) : Exception(message);
