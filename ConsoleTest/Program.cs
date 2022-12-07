﻿using TqkLibrary.Proxy;
using System.Net;
using TqkLibrary.Proxy.ProxyServers;
using TqkLibrary.Proxy.ProxySources;
using System.IO;
using System.Net.Http.Headers;
using TqkLibrary.Proxy.Interfaces;

IProxySource proxySource = new LocalHttpProxySource();

const string address = "127.0.0.1:13566";
HttpProxyServer httpProxyServer = new HttpProxyServer(IPEndPoint.Parse(address), proxySource);
httpProxyServer.StartListen();

using HttpClientHandler httpClientHandler = new HttpClientHandler()
{
    Proxy = new WebProxy()
    {
        Address = new Uri($"http://{address}"),
    },
    UseCookies = false,
};
using HttpClient httpClient = new HttpClient(httpClientHandler, false);
//using var res = await httpClient.GetAsync("http://ip-api.com/json");
//string res_content = await res.Content.ReadAsStringAsync();
//using var res2 = await httpClient.GetAsync("https://youtube.com/c/MuseVi%E1%BB%87tNam");
//string res2_content = await res2.Content.ReadAsStringAsync();

{
    using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "http://ip-api.com/json");
    httpRequestMessage.Headers.Add("Accept", "application/json");
    httpRequestMessage.Headers.Add("Cookie", "GeoIP=VN:35:Da_Lat:11.94:108.42:v4; enwikimwuser-sessionId=78f68a694551e47537e4; WMF-Last-Access=06-Dec-2022; WMF-Last-Access-Global=06-Dec-2022; enwikiwmE-sessionTickLastTickTime=1670355455366; enwikiwmE-sessionTickTickCount=14");
    httpRequestMessage.Content = new StringContent("Test post");
    using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage,HttpCompletionOption.ResponseHeadersRead);
    string content = await httpResponseMessage.Content.ReadAsStringAsync();
}

Console.ReadLine();
httpProxyServer.StopListen();

Console.ReadLine();