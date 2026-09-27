extern alias ProxyManagerApp;
using ProxyManagerApp::West94.AspNetCore.Authentication;
using System.Net;

namespace West94.ProxyManager.API.Tests.Unit.Authentication;

[Trait("Category", "Unit")]
public class OidcTokenRefresherTests
{
    private const string SuccessBody =
        """{"access_token":"new-access","refresh_token":"new-refresh","id_token":"new-id","expires_in":300}""";

    [Fact]
    public async Task RefreshAsync_Success_ParsesResponseAndPostsClientCredentials()
    {
        var endpoint = new TokenEndpointFakes.FakeTokenEndpoint(HttpStatusCode.OK, SuccessBody);
        var refresher = TokenEndpointFakes.CreateRefresher(endpoint);

        var result = await refresher.RefreshAsync("old-refresh", TestContext.Current.CancellationToken);

        Assert.Equal(new TokenRefreshResult("new-access", "new-refresh", "new-id", 300), result);
        Assert.Equal(TokenEndpointFakes.TokenEndpoint, endpoint.LastRequestUri);
        var form = endpoint.LastForm!;
        Assert.Contains("grant_type=refresh_token", form);
        Assert.Contains("refresh_token=old-refresh", form);
        Assert.Contains("client_id=ypm", form);
        Assert.Contains("client_secret=s3cret", form);
    }

    [Fact]
    public async Task RefreshAsync_ErrorResponse_ReturnsNullAndAllowsRetry()
    {
        var endpoint = new TokenEndpointFakes.FakeTokenEndpoint(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
        var refresher = TokenEndpointFakes.CreateRefresher(endpoint);

        var first = await refresher.RefreshAsync("old-refresh", TestContext.Current.CancellationToken);
        var second = await refresher.RefreshAsync("old-refresh", TestContext.Current.CancellationToken);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(2, endpoint.CallCount);
    }

    [Fact]
    public async Task RefreshAsync_ParallelCallsWithSameToken_MakeOneHttpCall()
    {
        var endpoint = new TokenEndpointFakes.FakeTokenEndpoint(HttpStatusCode.OK, SuccessBody, delay: TimeSpan.FromMilliseconds(100));
        var refresher = TokenEndpointFakes.CreateRefresher(endpoint);

        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => refresher.RefreshAsync("old-refresh", TestContext.Current.CancellationToken))));

        Assert.Equal(1, endpoint.CallCount);
        Assert.All(results, r => Assert.Same(results[0], r));
        Assert.NotNull(results[0]);
    }
}
