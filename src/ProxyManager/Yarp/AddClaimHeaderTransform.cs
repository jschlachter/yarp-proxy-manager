using Yarp.ReverseProxy.Transforms;

namespace West94.ProxyManager.Yarp;

internal sealed class AddClaimHeaderTransform(string headerName, string claimType) : RequestTransform
{
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        if (context.HttpContext.User.Identity is not { IsAuthenticated: true })
        {
            return ValueTask.CompletedTask;
        }

        var values = context.HttpContext.User.FindAll(claimType).Select(c => c.Value).ToList();
        if (values.Count > 0)
        {
            context.ProxyRequest.Headers.TryAddWithoutValidation(headerName, string.Join(", ", values));
        }

        return ValueTask.CompletedTask;
    }
}
