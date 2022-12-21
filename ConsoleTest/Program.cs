using ConsoleTest;

Uri uri0 = new Uri("http://127.0.0.1:13566");
Uri uri1 = new Uri("http://[::1]:13566");
Uri uri2 = new Uri("httpbin.org:80");//must ->http://httpbin.org:80
Uri uri3 = new Uri("http://httpbin.org");
Uri uri4 = new Uri("http://httpbin.org:8080");
Uri uri5 = new Uri("tcp://httpbin.org:8080");
Uri uri6 = new Uri("udp://httpbin.org:8080");
Uri uri7 = new Uri("tcp://127.0.0.1:13566");
Uri uri8 = new Uri("udp://[::1]:13566");


await DebugTest.Test();
//await RealTest.Test();
