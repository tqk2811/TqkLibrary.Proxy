using TqkLibrary.Proxy;
using System.Net;
using TqkLibrary.Proxy.ProxyServers;
using TqkLibrary.Proxy.ProxySources;
using System.IO;
using System.Net.Http.Headers;
using TqkLibrary.Proxy.Interfaces;

//const string address = "127.0.0.1:13566";
const string address = "[::1]:13566";
IProxySource proxySource = new LocalHttpProxySource();


CredentialCache credentialCache = new CredentialCache();
NetworkCredential networkCredential = new NetworkCredential("admin", "admin");
credentialCache.Add(new Uri($"http://{address}"), "Basic", networkCredential);


HttpProxyServer httpProxyServer = new HttpProxyServer(IPEndPoint.Parse(address), proxySource, networkCredential);
httpProxyServer.StartListen();

using HttpClientHandler httpClientHandler = new HttpClientHandler()
{
    Proxy = new WebProxy()
    {
        Address = new Uri($"http://{address}"),
    },
    UseProxy = true,
    UseCookies = false,
    DefaultProxyCredentials = networkCredential,
    
};
using HttpClient httpClient = new HttpClient(httpClientHandler, false);


//{
//    using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://26.64.24.5/get");
//    using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
//    string content = await httpResponseMessage.Content.ReadAsStringAsync();
//}

//{
//    using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://httpbin.org/get");
//    using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
//    string content = await httpResponseMessage.EnsureSuccessStatusCode().Content.ReadAsStringAsync();
//}

{
    using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "http://httpbin.org/post");
    httpRequestMessage.Headers.Add("Accept", "application/json");
    httpRequestMessage.Content = new StringContent("Test post");
    using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
    string content = await httpResponseMessage.EnsureSuccessStatusCode().Content.ReadAsStringAsync();
}

{
    using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://www.youtube.com/c/MuseVi%E1%BB%87tNam");
    using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
    string content = await httpResponseMessage.EnsureSuccessStatusCode().Content.ReadAsStringAsync();
}

{
    using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://httpbin.org/post");
    httpRequestMessage.Headers.Add("Accept", "application/json");
    httpRequestMessage.Headers.Add("Cookie", "GeoIP=VN:35:Da_Lat:11.94:108.42:v4; enwikimwuser-sessionId=78f68a694551e47537e4; WMF-Last-Access=06-Dec-2022; WMF-Last-Access-Global=06-Dec-2022; enwikiwmE-sessionTickLastTickTime=1670355455366; enwikiwmE-sessionTickTickCount=14");
    httpRequestMessage.Content = new StringContent("Test post");
    using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
    string content = await httpResponseMessage.EnsureSuccessStatusCode().Content.ReadAsStringAsync();
}


Console.ReadLine();
httpProxyServer.StopListen();

Console.ReadLine();