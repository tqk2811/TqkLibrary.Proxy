using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxyServers;
using TqkLibrary.Proxy.ProxySources;

namespace TestProxy.HttpProxyServerTest
{
    [TestClass]
    public class HttpProxySourceIpV6Test
    {
        static readonly IProxySource localProxySource;

        static readonly HttpClientHandler httpClientHandler;
        static readonly HttpClient httpClient;

        static HttpProxySourceIpV6Test()
        {
            localProxySource = new LocalHttpProxySource();

            httpClientHandler = new HttpClientHandler()
            {
                Proxy = new WebProxy()
                {
                    Address = new Uri($"http://{Singleton.AddressIpv6_1}"),
                },
                UseCookies = false,
            };
            httpClient = new HttpClient(httpClientHandler, false);
        }


        [TestMethod]
        public async Task HttpGet()
        {
            using var httpProxyServer = new HttpProxyServer(IPEndPoint.Parse(Singleton.AddressIpv6_0), localProxySource);//self host
            httpProxyServer.StartListen();

            HttpProxySource httpProxySource = new HttpProxySource(new Uri($"http://{Singleton.AddressIpv6_0}"));//connect to self host
            using var httpProxyServer1 = new HttpProxyServer(IPEndPoint.Parse(Singleton.AddressIpv6_1), localProxySource);//create new server on Address1
            httpProxyServer1.StartListen();


            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://httpbin.org/get");
            using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.AreEqual(json["url"].ToString(), "http://httpbin.org/get");
        }

        [TestMethod]
        public async Task HttpGetTwoTimes()
        {
            using var httpProxyServer = new HttpProxyServer(IPEndPoint.Parse(Singleton.AddressIpv6_0), localProxySource);//self host
            httpProxyServer.StartListen();

            HttpProxySource httpProxySource = new HttpProxySource(new Uri($"http://{Singleton.AddressIpv6_0}"));//connect to self host
            using var httpProxyServer1 = new HttpProxyServer(IPEndPoint.Parse(Singleton.AddressIpv6_1), localProxySource);//create new server on Address1
            httpProxyServer1.StartListen();


            {
                using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://httpbin.org/get");
                using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
                string content = await httpResponseMessage.Content.ReadAsStringAsync();
                dynamic json = JsonConvert.DeserializeObject(content);
                Assert.AreEqual(json["url"].ToString(), "http://httpbin.org/get");
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
            using var httpProxyServer = new HttpProxyServer(IPEndPoint.Parse(Singleton.AddressIpv6_0), localProxySource);//self host
            httpProxyServer.StartListen();

            HttpProxySource httpProxySource = new HttpProxySource(new Uri($"http://{Singleton.AddressIpv6_0}"));//connect to self host
            using var httpProxyServer1 = new HttpProxyServer(IPEndPoint.Parse(Singleton.AddressIpv6_1), localProxySource);//create new server on Address1
            httpProxyServer1.StartListen();


            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "http://httpbin.org/post");
            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Content = new StringContent("Test post");
            using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.AreEqual(json["url"].ToString(), "http://httpbin.org/post");
            Assert.AreEqual(json["data"].ToString(), "Test post");
        }


        [TestMethod]
        public async Task HttpsGet()
        {
            using var httpProxyServer = new HttpProxyServer(IPEndPoint.Parse(Singleton.AddressIpv6_0), localProxySource);//self host
            httpProxyServer.StartListen();

            HttpProxySource httpProxySource = new HttpProxySource(new Uri($"http://{Singleton.AddressIpv6_0}"));//connect to self host
            using var httpProxyServer1 = new HttpProxyServer(IPEndPoint.Parse(Singleton.AddressIpv6_1), localProxySource);//create new server on Address1
            httpProxyServer1.StartListen();


            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://httpbin.org/get");
            using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.AreEqual(json["url"].ToString(), "https://httpbin.org/get");
        }

        [TestMethod]
        public async Task HttpsPost()
        {
            using var httpProxyServer = new HttpProxyServer(IPEndPoint.Parse(Singleton.AddressIpv6_0), localProxySource);//self host
            httpProxyServer.StartListen();

            HttpProxySource httpProxySource = new HttpProxySource(new Uri($"http://{Singleton.AddressIpv6_0}"));//connect to self host
            using var httpProxyServer1 = new HttpProxyServer(IPEndPoint.Parse(Singleton.AddressIpv6_1), localProxySource);//create new server on Address1
            httpProxyServer1.StartListen();


            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://httpbin.org/post");
            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Content = new StringContent("Test post");
            using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            Assert.AreEqual(json["url"].ToString(), "https://httpbin.org/post");
            Assert.AreEqual(json["data"].ToString(), "Test post");
        }
    }
}
