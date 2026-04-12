using System.Text.Json;

namespace West94.AspNetCore.Authentication;

internal class TokenClient
{
    readonly HttpClient _httpClient;
    readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    
    public TokenClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AccessTokenResponse?> RefreshToken(string refreshToken, string? domain)
    {
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = "proxy-manager"
        };

        var discoveryResponse = await _httpClient.GetAsync($"{domain}.well-known/openid-configuration");
        discoveryResponse.EnsureSuccessStatusCode();

        var discoveryContent = await discoveryResponse.Content.ReadAsStringAsync();
        var discoveryDocument = JsonSerializer.Deserialize<JsonElement>(discoveryContent, _jsonSerializerOptions);
        var tokenEndpoint = discoveryDocument.GetProperty("token_endpoint").GetString();

        if (string.IsNullOrEmpty(tokenEndpoint))
        {
            throw new InvalidOperationException("Token endpoint not found in discovery document");
        }

        var requestContent = new FormUrlEncodedContent(body.Select(p => new KeyValuePair<string, string>(p.Key, p.Value ?? "")));
        using(var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint) { Content = requestContent})
        {
            using(var repsonse = await _httpClient.SendAsync(request).ConfigureAwait(false))
            {
                if (!repsonse.IsSuccessStatusCode)
                {
                    return null;
                }

                var contentStream = await repsonse.Content.ReadAsStreamAsync().ConfigureAwait(false);

                return await JsonSerializer.DeserializeAsync<AccessTokenResponse>(contentStream, _jsonSerializerOptions).ConfigureAwait(false);
            }
        }
    }
}