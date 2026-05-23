using Newtonsoft.Json;
using TqkLibrary.Proxy;

namespace TestProxy
{
    public abstract class BaseConnectTest : BaseClassTest
    {
        // httpbin.org rate-limits GitHub Actions IPs (returns HTML 5xx); httpbingo.org is a more reliable
        // drop-in replacement maintained by the original author. Override via TESTPROXY_HTTPBIN env if needed.
        static readonly string testDomain = Environment.GetEnvironmentVariable("TESTPROXY_HTTPBIN") ?? "httpbingo.org";
        const int MaxAttempts = 3;
        readonly HttpClient _httpClient;
        public BaseConnectTest()
        {
            _httpClient = new HttpClient(CreateHttpMessageHandler(_proxyServer), true);
        }
        protected override void Dispose(bool isDisposing)
        {
            _httpClient.Dispose();
            base.Dispose(isDisposing);
        }
        protected abstract HttpMessageHandler CreateHttpMessageHandler(ProxyServer baseProxyServer);

        // httpbin.org occasionally returns 5xx HTML error pages; retry transient failures so external flakes don't fail the suite.
        async Task<string> SendForJsonAsync(Func<HttpRequestMessage> requestFactory)
        {
            Exception? lastException = null;
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    using HttpRequestMessage request = requestFactory();
                    using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    string content = await response.Content.ReadAsStringAsync();
                    response.EnsureSuccessStatusCode();
                    if (content.Length == 0 || content[0] != '{')
                        throw new InvalidOperationException($"Expected JSON response, got: {content.Substring(0, Math.Min(content.Length, 200))}");
                    return content;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is InvalidOperationException)
                {
                    lastException = ex;
                    if (attempt < MaxAttempts)
                        await Task.Delay(TimeSpan.FromSeconds(attempt));
                }
            }
            throw lastException!;
        }

        async Task<string> SendForTextAsync(Func<HttpRequestMessage> requestFactory)
        {
            Exception? lastException = null;
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    using HttpRequestMessage request = requestFactory();
                    using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    string content = await response.Content.ReadAsStringAsync();
                    response.EnsureSuccessStatusCode();
                    return content;
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    if (attempt < MaxAttempts)
                        await Task.Delay(TimeSpan.FromSeconds(attempt));
                }
            }
            throw lastException!;
        }


        [TestMethod]
        public async Task HttpGet()
        {
            string content = await SendForJsonAsync(() => new HttpRequestMessage(HttpMethod.Get, $"http://{testDomain}/get"));
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.IsNotNull(json);
            Assert.AreEqual(json["url"]?.ToString(), $"http://{testDomain}/get");
        }

        [TestMethod]
        public async Task HttpGetTwoTimes()
        {
            {
                string content = await SendForJsonAsync(() => new HttpRequestMessage(HttpMethod.Get, $"http://{testDomain}/get"));
                dynamic json = JsonConvert.DeserializeObject(content);
                Assert.IsNotNull(json);
                Assert.AreEqual(json["url"]?.ToString(), $"http://{testDomain}/get");
            }

            //Test make new request on 1 connection with proxy
            {
                //github will redirect (301) http -> https -> new connection proxy using CONNECT method
                string content = await SendForTextAsync(() => new HttpRequestMessage(HttpMethod.Get, "http://tqk2811.github.io/TqkLibrary.Proxy/Test.txt"));
                Assert.AreEqual(content, "TqkLibrary.Proxy data");
            }
        }

        [TestMethod]
        public async Task HttpPost()
        {
            string content = await SendForJsonAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"http://{testDomain}/post");
                request.Headers.Add("Accept", "application/json");
                request.Content = new StringContent("Test post");
                return request;
            });
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.IsNotNull(json);
            Assert.AreEqual(json["url"]?.ToString(), $"http://{testDomain}/post");
            Assert.AreEqual(json["data"]?.ToString(), "Test post");
        }


        [TestMethod]
        public async Task HttpsGet()
        {
            string content = await SendForJsonAsync(() => new HttpRequestMessage(HttpMethod.Get, $"https://{testDomain}/get"));
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.IsNotNull(json);
            Assert.AreEqual(json["url"]?.ToString(), $"https://{testDomain}/get");
        }

        [TestMethod]
        public async Task HttpsPost()
        {
            string content = await SendForJsonAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"https://{testDomain}/post");
                request.Headers.Add("Accept", "application/json");
                request.Content = new StringContent("Test post");
                return request;
            });
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.IsNotNull(json);
            Assert.AreEqual(json["url"]?.ToString(), $"https://{testDomain}/post");
            Assert.AreEqual(json["data"]?.ToString(), "Test post");
        }
    }
}
