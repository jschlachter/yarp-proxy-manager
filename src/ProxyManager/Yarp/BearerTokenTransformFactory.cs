using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace West94.ProxyManager.Yarp;

internal sealed class BearerTokenTransformFactory : ITransformFactory
{
    private const string BearerTokenKey = "BearerToken";

    public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        return transformValues.ContainsKey(BearerTokenKey);
    }

    public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (!transformValues.TryGetValue(BearerTokenKey, out var tokenName))
            return false;

        if (string.IsNullOrWhiteSpace(tokenName))
            tokenName = "access_token";

        context.RequestTransforms.Add(new AddBearerTokenToHeadersTransform(tokenName));
        return true;
    }
}
