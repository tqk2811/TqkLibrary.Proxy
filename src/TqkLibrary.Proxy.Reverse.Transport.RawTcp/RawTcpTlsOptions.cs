using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace TqkLibrary.Proxy.Reverse.Transport.RawTcp
{
    /// <summary>TLS configuration for the RawTcp transport. Leave null on both sides to disable TLS.</summary>
    public sealed class RawTcpTlsOptions
    {
        // ---- server ----
        public X509Certificate2? ServerCertificate { get; set; }
        public bool RequireClientCertificate { get; set; }
        public RemoteCertificateValidationCallback? ValidateClientCertificate { get; set; }

        // ---- client ----
        public string? TargetHost { get; set; }
        public X509CertificateCollection? ClientCertificates { get; set; }
        public RemoteCertificateValidationCallback? ValidateServerCertificate { get; set; }

        public System.Security.Authentication.SslProtocols Protocols { get; set; } =
            System.Security.Authentication.SslProtocols.None;
    }
}
