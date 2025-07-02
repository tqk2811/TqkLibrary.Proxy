using Newtonsoft.Json;
using TqkLibrary.Proxy;

namespace TestProxy
{
    public abstract class BaseConnectTest : BaseClassTest
    {
        //const string testDomain = "httpbingo.org";
        const string testDomain = "httpbin.org";
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


        [TestMethod]
        public async Task HttpGet()
        {
            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, $"http://{testDomain}/get");
            using HttpResponseMessage httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.IsNotNull(json);
            Assert.AreEqual(json["url"]?.ToString(), $"http://{testDomain}/get");
        }

        [TestMethod]
        public async Task HttpGetTwoTimes()
        {
            {
                using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, $"http://{testDomain}/get");
                using HttpResponseMessage httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
                string content = await httpResponseMessage.Content.ReadAsStringAsync();
                dynamic json = JsonConvert.DeserializeObject(content);
                Assert.IsNotNull(json);
                Assert.AreEqual(json["url"]?.ToString(), $"http://{testDomain}/get");
            }

            //Test make new request on 1 connection with proxy
            {
                //github will redirect (301) http -> https -> new connection proxy using CONNECT method
                using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://tqk2811.github.io/TqkLibrary.Proxy/Test.txt");
                using HttpResponseMessage httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
                string content = await httpResponseMessage.Content.ReadAsStringAsync();
                Assert.AreEqual(content, "TqkLibrary.Proxy data");
            }
        }

        [TestMethod]
        public async Task HttpPost()
        {
            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"http://{testDomain}/post");
            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Content = new StringContent("Test post");
            using HttpResponseMessage httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.IsNotNull(json);
            Assert.AreEqual(json["url"]?.ToString(), $"http://{testDomain}/post");
            Assert.AreEqual(json["data"]?.ToString(), "Test post");
        }


        [TestMethod]
        public async Task HttpsGet()
        {
            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, $"https://{testDomain}/get");
            using HttpResponseMessage httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.IsNotNull(json);
            Assert.AreEqual(json["url"]?.ToString(), $"https://{testDomain}/get");
        }

        [TestMethod]
        public async Task HttpsPost()
        {
            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"https://{testDomain}/post");
            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Content = new StringContent("Test post");
            using HttpResponseMessage httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.IsNotNull(json);
            Assert.AreEqual(json["url"]?.ToString(), $"https://{testDomain}/post");
            Assert.AreEqual(json["data"]?.ToString(), "Test post");
        }
    }
}
