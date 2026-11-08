# Backlog

Việc còn treo và việc nên làm của thư viện. Xử lý xong mục nào thì xoá mục đó trong cùng commit với bản sửa.

Phần lớn mục chuyển từ backlog của app ProxyDivert ngày 2026-11-08. Mục mang mã D/E đến từ đợt rà soát ngày 2026-09-08: D = lỗi TqkLibrary.Proxy, E = thiết kế (OOP, tái sử dụng, test). Mức `Cao`/`Vừa`/`Thấp` theo nhãn trong tiêu đề; mục E: `Must` = đã gây lỗi thật hoặc chặn việc mở rộng, `Should` = giảm trùng lặp hoặc cho phép test, `Nice` = sạch hơn, không đổi hành vi. Đường dẫn `src/ProxyDivert.*` là của repo app ProxyDivert (repo cha, nhúng thư viện này làm submodule); đường dẫn khác tính từ gốc repo này.

### Kiểm chống DPI trên mạng thật

- **Vấn đề:** cờ Anti-DPI (tách tên miền trong CONNECT, tách ClientHello thành nhiều TLS record quanh SNI) mới được kiểm bằng test offline. Một server TLS thật (SslStream/SChannel trên loopback) đã nhận ClientHello bị tách và bắt tay xong. Còn hai câu hỏi chưa ai trả lời: nhà mạng đang chặn có bị lách thật không, và ra dây mỗi record có thành segment riêng không (bắt bằng Wireshark).
- **Vị trí:** `src/TqkLibrary.Proxy/StreamHelpers/TlsHandshakeChunkingStream.cs`, `StreamExtensions.cs` (`WriteSplitAroundAsync`).
- **Vì sao:** DPI có trạng thái (ráp lại TCP, ráp cả các mảnh handshake) thì cách tách record không qua được; khi đó cần thêm mức gói qua WinDivert (gói giả TTL thấp, checksum sai) như GoodbyeDPI.
- **Ngày ghi:** 2026-10-03.

### Chế độ dự phòng "chỉ tách segment" cho Anti-DPI

- **Vấn đề:** vài server hoặc middlebox cũ (cân bằng tải, TLS nhúng) có thể từ chối ClientHello bị tách thành nhiều record, dù RFC 8446 cho phép. Hiện chưa có chế độ chỉ tách TCP segment mà giữ nguyên record. Ngoài ra, ClientHello gửi lại sau HelloRetryRequest đi nguyên, không bị tách.
- **Vị trí:** `TlsHandshakeChunkingStream.cs` (`SplitAroundServerName`), cờ `Outbound.AntiDpiTls` ở `src/ProxyDivert.Core/Routing/Models/Outbound.cs`.
- **Vì sao:** finding của review ngày 2026-10-03; chỉ đáng làm nếu gặp trang hỏng khi bật cờ.
- **Ngày ghi:** 2026-10-03.

### D5. Nhóm TqkLibrary.Proxy core, phía client ProxyDivert dùng (Thấp)

- **Vấn đề:**
  - `HttpProxySource` dùng `_proxy.Host` (giữ ngoặc `[::1]`) thay vì `DnsSafeHost` như Socks4/5: [HttpProxySource.ConnectTunnel.cs:39-41](../src/TqkLibrary.Proxy/ProxySources/HttpProxySource.ConnectTunnel.cs#L39-L41). HTTP proxy cấu hình bằng literal IPv6 resolve thất bại.
  - CONNECT không gửi header `Host` (RFC 7230 bắt buộc): [HttpProxySource.ConnectTunnel.cs:72-80](../src/TqkLibrary.Proxy/ProxySources/HttpProxySource.ConnectTunnel.cs#L72-L80).
  - `BaseProxySourceTunnel` finalizer gọi `Dispose(false)` nhưng override vẫn chạm object managed: [BaseProxySourceTunnel.cs:16-25](../src/TqkLibrary.Proxy/ProxySources/BaseProxySourceTunnel.cs#L16-L25), [Socks5ProxySource.BaseTunnel.cs](../src/TqkLibrary.Proxy/ProxySources/Socks5ProxySource.BaseTunnel.cs). Đường tới được: A22.
  - Nhánh netstandard2.0 không có timeout connect/handshake, một số chỗ không truyền token: [Socks5ProxySource.BaseTunnel.cs](../src/TqkLibrary.Proxy/ProxySources/Socks5ProxySource.BaseTunnel.cs), [Socks4ProxySource.BaseTunnel.cs](../src/TqkLibrary.Proxy/ProxySources/Socks4ProxySource.BaseTunnel.cs).
  - `LocalProxySource` UDP chỉ IPv4 dù `IsSupportIpv6 = true`: [LocalProxySource.cs](../src/TqkLibrary.Proxy/ProxySources/LocalProxySource.cs), [LocalProxySource.UdpTunnel.cs](../src/TqkLibrary.Proxy/ProxySources/LocalProxySource.UdpTunnel.cs). Chỉ chạm nếu UDP Direct đi qua relay thay vì passthrough.
  - `LocalProxySource.ConnectTunnel`: `AllowIpv6 = false` mà host chỉ có AAAA thì `ConnectAsync(mảng rỗng)` ném `ArgumentException` khó hiểu: [LocalProxySource.ConnectTunnel.cs:70-84](../src/TqkLibrary.Proxy/ProxySources/LocalProxySource.ConnectTunnel.cs#L70-L84).
  - Chỉ 26/223 `await` có `ConfigureAwait(false)` (số đếm 2026-09-11); `async void _MainLoopListen`: [ProxyServer.cs:166](../src/TqkLibrary.Proxy/ProxyServer.cs#L166). Nhánh Test ở [OutboundTester.cs:51-52](../../../src/ProxyDivert.Core/Outbounds/OutboundTester.cs#L51-L52) gọi từ UI, `ConfigureAwait(false)` đã có ở app nhưng continuation nội bên trong lib bị post về Dispatcher.
- **Vị trí:** xem từng gạch.
- **Ngày ghi:** 2026-09-08 (kiểm lại 2026-09-11 và 2026-10-09).

### D6. Phía server TqkLibrary.Proxy (chỉ `ProxyDivert.Cli --self-host-port`)

- **Vấn đề:** còn 7 mục (6 mục đã sửa trong `fix/wave2`). Test có sẵn ở `src/TestProxy/ServerTest/HttpProxyServerForwardingTest.cs` và `Socks5FailureReplyTest.cs`.
  - **Vừa**, nhánh 1xx: `_ResponseCanHaveBody` ([HttpProxyServer.cs:245-250](../src/TqkLibrary.Proxy/ProxyServers/HttpProxyServer.cs#L245-L250)) coi 1xx là response không body và kết thúc `_HttpTransfer`; `source_stream` bị dispose trước khi origin gửi response thật, nên client gửi `Expect: 100-continue` mất response. Gặp 1xx thì đọc tiếp response kế trên cùng upstream.
  - **Vừa**, `GetProxySourceAsync`/`GetConnectSourceAsync` vẫn nằm ngoài `try` ([Socks5ProxyServer.cs:160-161](../src/TqkLibrary.Proxy/ProxyServers/Socks5ProxyServer.cs#L160-L161), [Socks4ProxyServer.cs:149-152](../src/TqkLibrary.Proxy/ProxyServers/Socks4ProxyServer.cs#L149-L152)): outbound không dựng được (wireproxy chưa lên, VPN chưa quay số) vẫn là "connection tự đứt không lời giải thích". Kéo hai lời gọi vào `try`; thêm test cho nhánh SOCKS4.
  - **Vừa**, BIND thiếu reply thứ hai cả hai phía: `Socks5ProxyServer`, `Socks4ProxyServer` và `Socks5ProxySource.BindTunnel`. ProxyDivert không dùng BIND ở đường nào và không có cách kiểm thật ngoài tự viết cả hai đầu nên chưa làm; cần sửa đồng bộ cả ba.
  - **Vừa**, `PreReadAsync` đọc tới khi một lượt không thêm byte ([PreReadStream.cs](../src/TqkLibrary.Proxy/StreamHelpers/PreReadStream.cs), `PreReadAsync` dòng 17, `PreReadLineAsync` dòng 53): peer gửi vài byte không CRLF rồi im thì `PreReadLineAsync` chờ vô hạn; `tcpClient.ReceiveTimeout` ([ProxyServer.cs:208](../src/TqkLibrary.Proxy/ProxyServer.cs#L208)) không áp cho `ReadAsync`, nên slowloris giữ slot. Cần timeout riêng cho giai đoạn nhận diện giao thức. Nhánh SOCKS không chạm (nhận diện bằng 1 byte).
  - **Thấp**, `HeaderRequestParse`/`HeaderResponseParse` `Split(':')` vứt header có dấu `:` trong value: [HeaderRequestParse.cs:68](../src/TqkLibrary.Proxy/Helpers/HeaderRequestParse.cs#L68). `Split(':', 2)`.
  - **Thấp**, `client_isKeepAlive` gán sau `continue` của nhánh 407: [HttpProxyServer.cs:84-92](../src/TqkLibrary.Proxy/ProxyServers/HttpProxyServer.cs#L84-L92).
  - **Thấp**, input SOCKS5 dị dạng ném exception thay vì reply (NMETHODS=0, domain length=0, VER không kiểm): [Socks5ProxyServer.cs](../src/TqkLibrary.Proxy/ProxyServers/Socks5ProxyServer.cs), [Socks5_DSTADDR.cs](../src/TqkLibrary.Proxy/Helpers/Socks5_DSTADDR.cs).
- **Vị trí:** xem từng gạch.
- **Ngày ghi:** 2026-09-08 (kiểm lại 2026-09-11 và 2026-10-09).

### D7. Các project TqkLibrary.Proxy mà ProxyDivert không dùng (SshCli, GlobalUnicast, Reverse)

- **Vấn đề:** lỗi thư viện, ProxyDivert không chạm tới (SSH dùng `SshNet`, không phải `SshCli`); ghi lại vì là lỗi thật của thư viện. Chưa dò lại từng dòng ngày 2026-10-09, tra theo ký hiệu. Mọi file nằm dưới `src/` của repo này.
  - **Cao**, SshCli: stdio của tiến trình ControlMaster redirect nhưng không đọc, ssh deadlock khi pipe đầy (`TqkLibrary.Proxy.SshCli/SshProcessRunner.cs`). Dùng `redirect: false` cho master.
  - **Cao**, GlobalUnicast: offset xoá địa chỉ sai (8 thay vì 6) và chọn NIC không lọc khi xoá, nên IPv6 tạm không bao giờ gỡ (`SlaccHelper.cs`).
  - **Cao**, GlobalUnicast: `SemaphoreSlim` static bị dispose theo instance, finalizer `Wait()` trên nó (`GlobalUnicastProxySource.cs`).
  - **Cao**, Reverse: `StreamCopy.PumpAsync` `WhenAny` rồi dispose cả hai, cắt cụt response (`Reverse/Client/StreamCopy.cs`); cùng gốc với E6.7.
  - **Cao**, Reverse: data channel không xác thực (`AuthSignature` khai báo nhưng client không set, server không kiểm), transport mặc định plaintext (`ReverseServer.cs`, `ReverseClient.cs`, `Protocol/Payloads.cs`).
  - **Vừa**, SshCli: password ghi plaintext ra `%TEMP%` suốt đời runner, helper `.cmd` trong thư mục user ghi được (`SshProcessRunner.cs`); mỗi kết nối cõng cứng 500ms (`OpenSshConnectSource.cs`).
  - **Vừa**, Reverse: session đóng nhưng control channel không bao giờ `DisposeAsync` (`ReverseServer.cs`, `ReverseClientSession.cs`); accept không giới hạn và byte định danh kênh không timeout, slowloris (`RawTcpReverseTransportServer.cs`, `HttpListenerWebSocketTransportServer.cs`); AspNetCore dispose `WebSocket` do framework sở hữu, cần `ownsSocket: false` (`AspNetCoreReverseTransportServer.cs`); accept loop HttpListener `catch { break; }` chết vì một exception lẻ; backoff không reset sau khi kết nối thành công, reset khi nhận `HelloAck` (`ReverseClient.cs`); `DialBindAsync` rò listener trên `Any:<random>` khi gửi `BindReady` thất bại.
  - **Vừa**, GlobalUnicast: interface ID sinh bằng `new Random()` (netstandard2.0 seed theo tick), `Task.Delay(3000)` trong semaphore bỏ qua cancellation; chọn prefix sai (bỏ DHCPv6/Manual, nhận nhầm ULA, không loại tentative/deprecated, không refresh khi interface đổi) (`SlaccHelper.cs`).
  - **Thấp**, Reverse: `BindReady.Address` luôn `0.0.0.0`, `BindAccepted.PeerAddress` không điền; `ToEndPoint()` parse địa chỉ rác giết cả session; `_ = SendAsync` nuốt lỗi async; `HandleFrameAsync` không giới hạn đồng thời; `WebSocketStream` coi message rỗng là EOF; `RawTcp.StopAsync` không dispose `_cts`.
  - **Thấp**, GlobalUnicast: `Console.WriteLine` trong thư viện dù có `ILoggerFactory`; `ConnectTunnel` bỏ qua token, socket v6-only nên host chỉ có A record fail.
- **Vị trí:** xem từng gạch.
- **Ngày ghi:** 2026-09-08.

### E4.6. Exception là API nhưng không nhất quán, lib Proxy (Should)

- **Vấn đề:** server chỉ bắt `InitConnectSourceFailedException`; Socks5 auth fail ném `Exception` trần; Http ném không message, mất status; `WireGuardException : Exception` ngoài cây `ProxySourceException`; `InitConnectSourceFailedException` không có ctor `(message, inner)` nên app bọc mất inner (`VpnClientConnectSource.cs`). Hệ quả: `catch (Exception) when (isIpv6 ...)` trong [TcpConnectionRouter.cs:186-192](../../../src/ProxyDivert.Core/Engine/TcpConnectionRouter.cs#L186-L192) đánh dấu outbound "IPv4-only" kể cả khi lỗi là 407/auth/proxy chết.
- **Cách sửa:** cây `ProxySourceException` → `UpstreamUnreachableException`, `ProxyHandshakeException { Status }`, `ProxyAuthenticationException`, `DestinationUnreachableException`; mọi lớp có `(message, inner)`; `WireGuardException` vào cây. App: `NoteIpv6Failure` chỉ với `DestinationUnreachableException`/timeout; C8 (dial timeout thành "cancelled") sửa cùng.
- **Ngày ghi:** 2026-09-08.

### E4.7. Nhóm Nice (abstraction)

- **Phạm vi:** chỉ các gạch thuộc TqkLibrary.Proxy; các gạch còn lại của mục này nằm ở backlog app ProxyDivert (repo cha, `docs/Backlog-vi.md`, cùng tiêu đề).
- **Vấn đề:**
  - Marker interface rỗng và dán nhãn sai trong Proxy: `IHttpProxy/ISocks4Proxy/ISocks5Proxy/ISsh/IVpn/IAuthentication`; `LocalProxySource : IHttpProxy`, `WireGuardProxySource : ISocks5Proxy` lộ chi tiết cài đặt. Xoá hoặc cho một member thật.
  - Reverse: `event Func<…,Task>` chỉ await delegate cuối (`RawTcpReverseTransportServer.cs`); AspNetCore phải poll `socket.State` mỗi giây vì `IControlChannel` thiếu `Task Closed`.
  - `Singleton` limit toàn process trong Proxy (`Singleton.cs`); `LogCritical` cho mọi tunnel fail và header ở `Information` (`ProxyServer.cs`, `HttpProxyServer.cs`) là noise với hàng trăm tunnel/phút.
- **Vị trí:** xem từng gạch.
- **Ngày ghi:** 2026-09-08. Xen kẽ khi chạm file liên quan, không lên lịch riêng.

### E6.5. Tunnel client trong Proxy lặp 7 lần, base class rỗng mang finalizer (Should)

- **Vấn đề:** [BaseProxySourceTunnel.cs](../src/TqkLibrary.Proxy/ProxySources/BaseProxySourceTunnel.cs) rỗng; mỗi source (`Socks5ProxySource.BaseTunnel.cs`, `HttpProxySource.ConnectTunnel.cs`, `LocalProxySource.ConnectTunnel.cs`...) tự khai `TcpClient` + `Stream` + Dispose; guard `GetStreamAsync` "Mustbe run X first" giống hệt ở 7 chỗ; fallback "BND.ADDR = 0.0.0.0" chép 3 lần. ProxyDivert tạo 1 tunnel/1 kết nối TCP nên mỗi kết nối trả phí finalization queue vô ích.
- **Cách sửa:** `UpstreamTcpTunnel<TSource>` chứa `_tcpClient/_stream`, `ConnectUpstreamAsync`, `GetStreamAsync`, `Dispose(bool)` đúng cờ, bỏ finalizer; `Socks5ClientHandshake`/`Socks4ClientHandshake` static nhận `Stream` (test codec in-memory được). Bump minor vì `BaseTunnel` là public API.
- **Ngày ghi:** 2026-09-08.

### E6.6. WireProxyCli và SshCli copy-paste 5 tiện ích process (Should)

- **Vấn đề:** locate binary (`WireProxyProcessRunner.cs` ↔ `SshProcessRunner.cs`), `BuildPsi`/`BuildArgumentString`, chmod file tạm, stderr buffer, idiom Kill+Dispose ×5. Hình dạng khác nhau hợp lý (daemon + socket vs child-per-connection) nên cái dùng chung là helper, không phải base source.
- **Cách sửa:** `TqkLibrary.Proxy.Process` (hoặc internal + `InternalsVisibleTo`): `ExecutableLocator`, `ChildProcess` (Start, stderr tail giới hạn N dòng cuối, `Exited` không bắn khi Dispose, `KillAndDispose`, Job Object), `TempSecretFile` (tạo đúng quyền, xoá), `TcpListenerProbe`. `IChildProcess` là seam để test `WireGuardProxySource` không cần `wireproxy.exe`.
- **Ngày ghi:** 2026-09-08.

### E6.7. Relay hai chiều có ba bản với ngữ nghĩa half-close khác nhau (Should)

- **Vấn đề:** [StreamTransferHelper.cs](../src/TqkLibrary.Proxy/StreamHelpers/StreamTransferHelper.cs) (`WhenAll`, không đóng chiều kia), `Reverse/Client/StreamCopy.cs` (`WhenAny`, dispose cả hai), [ConnectSourceExtensions.cs](../../../src/ProxyDivert.Core/Outbounds/Extensions/ConnectSourceExtensions.cs) (app bọc thêm với chú thích "lib để boilerplate cho caller"). Liên quan C10.
- **Cách sửa:** một `StreamRelay` trong core Proxy với policy half-close rõ, extension `IConnectSource.RelayAsync(Stream, ...)` để app bỏ file này; Reverse dùng chung.
- **Ngày ghi:** 2026-09-08.

### E6.8. Nhóm nhỏ trùng lặp (Nice)

- **Phạm vi:** chỉ các gạch thuộc TqkLibrary.Proxy; các gạch còn lại của mục này nằm ở backlog app ProxyDivert (repo cha, `docs/Backlog-vi.md`, cùng tiêu đề).
- **Vấn đề:**
  - Bỏ ngoặc IPv6 `Trim('[', ']')` ở 7 chỗ và `host:port` qua `LastIndexOf(':')` ở 2 chỗ (`LocalProxySource.ConnectTunnel.cs`, `Socks5_DSTADDR.cs`, `OutboundUrl.cs`, `VpnProfileReader.cs`, `InTunnelResolver.cs`, `WireGuardConfigParser.cs`). Làm `EndPointParsing` trong core Proxy.
  - Credential từ `Uri.UserInfo` ba cách, độ đúng khác nhau (Http `Split(':')` không unescape): `HttpProxySource.cs`, `Socks5ProxySource.cs`, `Socks4ProxySource.cs`. Làm `ProxyUri.ParseCredential(Uri)`.
- **Vị trí:** xem từng gạch.
- **Ngày ghi:** 2026-09-08.

### E9.2. Seam test còn thiếu ở app và Proxy (Should)

- **Phạm vi:** chỉ các gạch thuộc TqkLibrary.Proxy; các gạch còn lại của mục này nằm ở backlog app ProxyDivert (repo cha, `docs/Backlog-vi.md`, cùng tiêu đề).
- **Vấn đề:**
  - Proxy lib: `TestProxy` = 6 test đánh internet thật, 0 test in-memory cho codec Socks4/5/HTTP; `TestProxy.Local` cần SSH/WG thật với mật khẩu hard-code (`OpenSshProxySourceTest.cs`; nên lấy từ env như `TESTPROXY_HTTPBIN`); Reverse 0 test dù chạy trên `Stream` (một duplex pipe + `StreamControlChannel` là đủ). E6.5 handshake static trên `Stream` mở đường test codec.
- **Vị trí:** xem từng gạch.
- **Ngày ghi:** 2026-09-08 (kiểm lại 2026-09-11).
