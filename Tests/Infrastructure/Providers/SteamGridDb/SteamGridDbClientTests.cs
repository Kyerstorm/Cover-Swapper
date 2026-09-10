using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using PluginCoverShuffle.Infrastructure.Providers.SteamGridDb;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Infrastructure.Providers.SteamGridDb
{
    public class SteamGridDbClientTests
    {
        private static HttpClient NewHttpClient(FakeHttpMessageHandler handler) => new HttpClient(handler);

        [Fact]
        public async Task SearchGamesAsync_WithNoApiKey_FailsWithMissingApiKey()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var client = new SteamGridDbClient(NewHttpClient(handler), () => null, new FakeCoverShuffleLogger());

            var result = await client.SearchGamesAsync("Cyberpunk 2077", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(SteamGridDbErrorKind.MissingApiKey, result.ErrorKind);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task SearchGamesAsync_SendsBearerAuthorizationHeader()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true,\"data\":[]}")
            });
            var client = new SteamGridDbClient(NewHttpClient(handler), () => "test-key", new FakeCoverShuffleLogger());

            await client.SearchGamesAsync("Cyberpunk 2077", CancellationToken.None);

            Assert.Single(handler.Requests);
            Assert.Equal(new AuthenticationHeaderValue("Bearer", "test-key"), handler.Requests[0].Headers.Authorization);
        }

        [Fact]
        public async Task SearchGamesAsync_ParsesSuccessfulGameMatches()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true,\"data\":[{\"id\":42,\"name\":\"Cyberpunk 2077\"}]}")
            });
            var client = new SteamGridDbClient(NewHttpClient(handler), () => "test-key", new FakeCoverShuffleLogger());

            var result = await client.SearchGamesAsync("Cyberpunk 2077", CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(result.Value);
            Assert.Equal(42, result.Value[0].Id);
            Assert.Equal("Cyberpunk 2077", result.Value[0].Name);
        }

        [Fact]
        public async Task SearchGamesAsync_On401_FailsWithInvalidApiKey()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"success\":false,\"errors\":[\"Invalid API Key\"]}")
            });
            var client = new SteamGridDbClient(NewHttpClient(handler), () => "bad-key", new FakeCoverShuffleLogger());

            var result = await client.SearchGamesAsync("Cyberpunk 2077", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(SteamGridDbErrorKind.InvalidApiKey, result.ErrorKind);
            Assert.DoesNotContain("bad-key", result.ErrorMessage);
        }

        [Fact]
        public async Task SearchGamesAsync_On500_FailsWithUnexpectedResponse()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            var client = new SteamGridDbClient(NewHttpClient(handler), () => "test-key", new FakeCoverShuffleLogger());

            var result = await client.SearchGamesAsync("Cyberpunk 2077", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(SteamGridDbErrorKind.UnexpectedResponse, result.ErrorKind);
        }

        [Fact]
        public async Task SearchGamesAsync_WithMalformedJson_FailsGracefully()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not json")
            });
            var client = new SteamGridDbClient(NewHttpClient(handler), () => "test-key", new FakeCoverShuffleLogger());

            var result = await client.SearchGamesAsync("Cyberpunk 2077", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(SteamGridDbErrorKind.UnexpectedResponse, result.ErrorKind);
        }

        [Fact]
        public async Task GetGridsForGameAsync_ParsesGrids()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true,\"data\":[{\"id\":7,\"url\":\"https://cdn/full.png\",\"thumb\":\"https://cdn/thumb.png\",\"width\":600,\"height\":900,\"style\":\"alternate\"}]}")
            });
            var client = new SteamGridDbClient(NewHttpClient(handler), () => "test-key", new FakeCoverShuffleLogger());

            var result = await client.GetGridsForGameAsync(42, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(result.Value);
            Assert.Equal(7, result.Value[0].Id);
            Assert.Equal("https://cdn/full.png", result.Value[0].Url);
            Assert.Equal("https://cdn/thumb.png", result.Value[0].Thumb);
        }

        [Fact]
        public async Task DownloadImageAsync_ReturnsBytesWithoutAuthorizationHeader()
        {
            var handler = new FakeHttpMessageHandler(request =>
            {
                Assert.Null(request.Headers.Authorization);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) };
            });
            var client = new SteamGridDbClient(NewHttpClient(handler), () => "test-key", new FakeCoverShuffleLogger());

            var result = await client.DownloadImageAsync("https://cdn/full.png", CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(new byte[] { 1, 2, 3 }, result.Value);
        }

        [Fact]
        public async Task ValidateApiKeyAsync_WhenAccepted_Succeeds()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true,\"data\":[]}")
            });
            var client = new SteamGridDbClient(NewHttpClient(handler), () => "test-key", new FakeCoverShuffleLogger());

            var result = await client.ValidateApiKeyAsync(CancellationToken.None);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task ValidateApiKeyAsync_WhenRejected_Fails()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
            var client = new SteamGridDbClient(NewHttpClient(handler), () => "bad-key", new FakeCoverShuffleLogger());

            var result = await client.ValidateApiKeyAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(SteamGridDbErrorKind.InvalidApiKey, result.ErrorKind);
        }
    }
}
