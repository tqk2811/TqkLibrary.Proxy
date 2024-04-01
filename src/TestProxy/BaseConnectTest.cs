using Newtonsoft.Json;
using TqkLibrary.Proxy.ProxyServers;

namespace TestProxy
{
    public abstract class BaseConnectTest : BaseClassTest
    {
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
            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://httpbin.org/get");
            using HttpResponseMessage httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.AreEqual(json["url"]?.ToString(), "http://httpbin.org/get");
        }

        [TestMethod]
        public async Task HttpGetTwoTimes()
        {
            {
                using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://httpbin.org/get");
                using HttpResponseMessage httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
                string content = await httpResponseMessage.Content.ReadAsStringAsync();
                dynamic json = JsonConvert.DeserializeObject(content);
                Assert.AreEqual(json["url"]?.ToString(), "http://httpbin.org/get");
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
            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "http://httpbin.org/post");
            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Content = new StringContent("Test post");
            using HttpResponseMessage httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.AreEqual(json["url"]?.ToString(), "http://httpbin.org/post");
            Assert.AreEqual(json["data"]?.ToString(), "Test post");
        }


        [TestMethod]
        public async Task HttpsGet()
        {
            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://httpbin.org/get");
            using HttpResponseMessage httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.AreEqual(json["url"]?.ToString(), "https://httpbin.org/get");
        }

        [TestMethod]
        public async Task HttpsPost()
        {
            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://httpbin.org/post");
            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Content = new StringContent("Test post");
            using HttpResponseMessage httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.AreEqual(json["url"]?.ToString(), "https://httpbin.org/post");
            Assert.AreEqual(json["data"]?.ToString(), "Test post");
        }
    }
}
