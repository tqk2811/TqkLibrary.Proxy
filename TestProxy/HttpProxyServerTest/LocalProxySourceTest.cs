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
using TqkLibrary.Proxy.Handlers;
using TqkLibrary.Proxy.Authentications;

namespace TestProxy.HttpProxyServerTest
{
    [TestClass]
    public class LocalProxySourceTest
    {
        static readonly IProxySource localProxySource;

        static readonly HttpClientHandler httpClientHandler;
        static readonly HttpClient httpClient;
        static readonly NetworkCredential networkCredential = new NetworkCredential("user", "password");
        static readonly HttpAuthenticationProxyServerHandler handler = new HttpAuthenticationProxyServerHandler();
        static LocalProxySourceTest()
        {
            localProxySource = new LocalProxySource();

            httpClientHandler = new HttpClientHandler()
            {
                Proxy = new WebProxy()
                {
                    Address = new Uri($"http://{Singleton.Address0}"),
                },
                UseCookies = false,
                UseProxy = true,
                DefaultProxyCredentials = networkCredential,
            };
            httpClient = new HttpClient(httpClientHandler, false);
            handler.WithAuthentications(networkCredential);
        }

        //------------------Connect Test----------------//
        [TestMethod]
        public async Task HttpGet()
        {
            using var httpProxyServer = new HttpProxyServer(IPEndPoint.Parse(Singleton.Address0), localProxySource, handler);
            httpProxyServer.StartListen();

            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://httpbin.org/get");
            using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.AreEqual(json["url"]?.ToString(), "http://httpbin.org/get");
        }

        [TestMethod]
        public async Task HttpGetTwoTimes()
        {
            using var httpProxyServer = new HttpProxyServer(IPEndPoint.Parse(Singleton.Address0), localProxySource, handler);
            httpProxyServer.StartListen();

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
            using var httpProxyServer = new HttpProxyServer(IPEndPoint.Parse(Singleton.Address0), localProxySource, handler);
            httpProxyServer.StartListen();

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
            using var httpProxyServer = new HttpProxyServer(IPEndPoint.Parse(Singleton.Address0), localProxySource, handler);
            httpProxyServer.StartListen();

            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://httpbin.org/get");
            using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.AreEqual(json["url"]?.ToString(), "https://httpbin.org/get");
        }

        [TestMethod]
        public async Task HttpsPost()
        {
            using var httpProxyServer = new HttpProxyServer(IPEndPoint.Parse(Singleton.Address0), localProxySource, handler);
            httpProxyServer.StartListen();

            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://httpbin.org/post");
            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Content = new StringContent("Test post");
            using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.AreEqual(json["url"]?.ToString(), "https://httpbin.org/post");
            Assert.AreEqual(json["data"]?.ToString(), "Test post");
        }


        //------------------Bind Test----------------//



    }
}
