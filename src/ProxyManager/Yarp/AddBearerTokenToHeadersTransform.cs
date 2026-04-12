using Microsoft.AspNetCore.Authentication;
using System.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;

namespace West94.ProxyManager.Yarp;

internal sealed class AddBearerTokenToHeadersTransform(string tokenName) : RequestTransform
{
    public override async ValueTask ApplyAsync(RequestTransformContext context)
    {
        if (context.HttpContext.User.Identity is not { IsAuthenticated: true })
        {
            return;
        }


        var token = await context.HttpContext.GetTokenAsync(tokenName);
        if (token is not null)
        {
            context.ProxyRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
