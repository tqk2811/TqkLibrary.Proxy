using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;
using TqkLibrary.Proxy.ProxyServers;
using Newtonsoft.Json;

namespace TestProxy.Socks4ProxyServerTest
{
    [TestClass]
    public class LocalProxySourceTest
    {
        static readonly IProxySource localProxySource;

        static readonly SocketsHttpHandler httpClientHandler;
        static readonly HttpClient httpClient;
        static LocalProxySourceTest()
        {
            localProxySource = new LocalProxySource();
            //.Net6 support socks4 and socks5
            //https://devblogs.microsoft.com/dotnet/dotnet-6-networking-improvements/#socks-proxy-support
            httpClientHandler = new SocketsHttpHandler()
            {
                Proxy = new WebProxy()
                {
                    Address = new Uri($"socks4://{Singleton.Address0}"),
                },
                UseCookies = false,
                UseProxy = true,
            };
            httpClient = new HttpClient(httpClientHandler, false);
        }

        [TestMethod]
        public async Task HttpGet()
        {
            using var socks4ProxyServer = new Socks4ProxyServer(IPEndPoint.Parse(Singleton.Address0), localProxySource);
            socks4ProxyServer.StartListen();

            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://httpbin.org/get");
            using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.AreEqual(json["url"]?.ToString(), "http://httpbin.org/get");
        }

        [TestMethod]
        public async Task HttpGetTwoTimes()
        {
            using var socks4ProxyServer = new Socks4ProxyServer(IPEndPoint.Parse(Singleton.Address0), localProxySource);
            socks4ProxyServer.StartListen();

            {
                using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://httpbin.org/get");
                using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
                string content = await httpResponseMessage.Content.ReadAsStringAsync();
                dynamic json = JsonConvert.DeserializeObject(content);
                Assert.AreEqual(json["url"]?.ToString(), "http://httpbin.org/get");
            }

            //Test make new request on 1 connection with proxy
            {
                //github will redirect (301) http -> https -> new connection proxy using CONNECT method
                using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://tqk2811.github.io/TqkLibrary.Proxy/Test.txt");
                using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
                string content = await httpResponseMessage.Content.ReadAsStringAsync();
                Assert.AreEqual(content, "TqkLibrary.Proxy data");
            }
        }

        [TestMethod]
        public async Task HttpPost()
        {
            using var socks4ProxyServer = new Socks4ProxyServer(IPEndPoint.Parse(Singleton.Address0), localProxySource);
            socks4ProxyServer.StartListen();

            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "http://httpbin.org/post");
            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Content = new StringContent("Test post");
            using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.AreEqual(json["url"]?.ToString(), "http://httpbin.org/post");
            Assert.AreEqual(json["data"]?.ToString(), "Test post");
        }


        [TestMethod]
        public async Task HttpsGet()
        {
            using var socks4ProxyServer = new Socks4ProxyServer(IPEndPoint.Parse(Singleton.Address0), localProxySource);
            socks4ProxyServer.StartListen();

            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://httpbin.org/get");
            using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.AreEqual(json["url"]?.ToString(), "https://httpbin.org/get");
        }

        [TestMethod]
        public async Task HttpsPost()
        {
            using var socks4ProxyServer = new Socks4ProxyServer(IPEndPoint.Parse(Singleton.Address0), localProxySource);
            socks4ProxyServer.StartListen();

            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://httpbin.org/post");
            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Content = new StringContent("Test post");
            using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.AreEqual(json["url"]?.ToString(), "https://httpbin.org/post");
            Assert.AreEqual(json["data"]?.ToString(), "Test post");
        }
    }
}
