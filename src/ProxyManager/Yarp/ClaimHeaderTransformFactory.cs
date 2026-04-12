using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace West94.ProxyManager.Yarp;

internal sealed class ClaimHeaderTransformFactory : ITransformFactory
{
    private const string ClaimHeaderKey = "ClaimHeader";
    private const string ClaimKey = "Claim";

    public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        return transformValues.ContainsKey(ClaimHeaderKey) && transformValues.ContainsKey(ClaimKey);
    }

    public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (!transformValues.TryGetValue(ClaimHeaderKey, out var headerName) ||
            !transformValues.TryGetValue(ClaimKey, out var claimType))
            return false;

        context.RequestTransforms.Add(new AddClaimHeaderTransform(headerName, claimType));
        return true;
    }
}
