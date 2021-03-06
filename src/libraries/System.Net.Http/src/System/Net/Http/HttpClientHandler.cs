// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net.Security;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http
{
    public partial class HttpClientHandler : HttpMessageHandler
    {
#if TARGETS_BROWSER
        private readonly BrowserHttpHandler _underlyingHandler;
#else
        private readonly SocketsHttpHandler _underlyingHandler;
#endif
        private readonly DiagnosticsHandler _diagnosticsHandler;
        private ClientCertificateOption _clientCertificateOptions;

        public HttpClientHandler()
        {
#if TARGETS_BROWSER
            _underlyingHandler = new BrowserHttpHandler();
#else
            _underlyingHandler = new SocketsHttpHandler();
#endif
            _diagnosticsHandler = new DiagnosticsHandler(_underlyingHandler);
            ClientCertificateOptions = ClientCertificateOption.Manual;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _underlyingHandler.Dispose();
            }

            base.Dispose(disposing);
        }

        public virtual bool SupportsAutomaticDecompression => true;
        public virtual bool SupportsProxy => true;
        public virtual bool SupportsRedirectConfiguration => true;

        public bool UseCookies
        {
            get => _underlyingHandler.UseCookies;
            set => _underlyingHandler.UseCookies = value;
        }

        public CookieContainer CookieContainer
        {
            get => _underlyingHandler.CookieContainer;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _underlyingHandler.CookieContainer = value;
            }
        }

        public DecompressionMethods AutomaticDecompression
        {
            get => _underlyingHandler.AutomaticDecompression;
            set => _underlyingHandler.AutomaticDecompression = value;
        }

        public bool UseProxy
        {
            get => _underlyingHandler.UseProxy;
            set => _underlyingHandler.UseProxy = value;
        }

        public IWebProxy? Proxy
        {
            get => _underlyingHandler.Proxy;
            set => _underlyingHandler.Proxy = value;
        }

        public ICredentials? DefaultProxyCredentials
        {
            get => _underlyingHandler.DefaultProxyCredentials;
            set => _underlyingHandler.DefaultProxyCredentials = value;
        }

        public bool PreAuthenticate
        {
            get => _underlyingHandler.PreAuthenticate;
            set => _underlyingHandler.PreAuthenticate = value;
        }

        public bool UseDefaultCredentials
        {
            // SocketsHttpHandler doesn't have a separate UseDefaultCredentials property.  There
            // is just a Credentials property.  So, we need to map the behavior.
            get => _underlyingHandler.Credentials == CredentialCache.DefaultCredentials;
            set
            {
                if (value)
                {
                    _underlyingHandler.Credentials = CredentialCache.DefaultCredentials;
                }
                else
                {
                    if (_underlyingHandler.Credentials == CredentialCache.DefaultCredentials)
                    {
                        // Only clear out the Credentials property if it was a DefaultCredentials.
                        _underlyingHandler.Credentials = null;
                    }
                }
            }
        }

        public ICredentials? Credentials
        {
            get => _underlyingHandler.Credentials;
            set => _underlyingHandler.Credentials = value;
        }

        public bool AllowAutoRedirect
        {
            get => _underlyingHandler.AllowAutoRedirect;
            set => _underlyingHandler.AllowAutoRedirect = value;
        }

        public int MaxAutomaticRedirections
        {
            get => _underlyingHandler.MaxAutomaticRedirections;
            set => _underlyingHandler.MaxAutomaticRedirections = value;
        }

        public int MaxConnectionsPerServer
        {
            get => _underlyingHandler.MaxConnectionsPerServer;
            set => _underlyingHandler.MaxConnectionsPerServer = value;
        }

        public int MaxResponseHeadersLength
        {
            get => _underlyingHandler.MaxResponseHeadersLength;
            set => _underlyingHandler.MaxResponseHeadersLength = value;
        }

        public ClientCertificateOption ClientCertificateOptions
        {
            get => _clientCertificateOptions;
            set
            {
                switch (value)
                {
                    case ClientCertificateOption.Manual:
#if TARGETS_BROWSER
                        _clientCertificateOptions = value;
#else
                        ThrowForModifiedManagedSslOptionsIfStarted();
                        _clientCertificateOptions = value;
                        _underlyingHandler.SslOptions.LocalCertificateSelectionCallback = (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => CertificateHelper.GetEligibleClientCertificate(ClientCertificates)!;
#endif
                        break;

                    case ClientCertificateOption.Automatic:
#if TARGETS_BROWSER
                        _clientCertificateOptions = value;
#else
                        ThrowForModifiedManagedSslOptionsIfStarted();
                        _clientCertificateOptions = value;
                        _underlyingHandler.SslOptions.LocalCertificateSelectionCallback = (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => CertificateHelper.GetEligibleClientCertificate()!;
#endif
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(value));
                }
            }
        }

        public X509CertificateCollection ClientCertificates
        {
            get
            {
                if (ClientCertificateOptions != ClientCertificateOption.Manual)
                {
                    throw new InvalidOperationException(SR.Format(SR.net_http_invalid_enable_first, nameof(ClientCertificateOptions), nameof(ClientCertificateOption.Manual)));
                }

                return _underlyingHandler.SslOptions.ClientCertificates ??
                    (_underlyingHandler.SslOptions.ClientCertificates = new X509CertificateCollection());
            }
        }

        public Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>? ServerCertificateCustomValidationCallback
        {
#if TARGETS_BROWSER
            get => throw new PlatformNotSupportedException();
            set => throw new PlatformNotSupportedException();
#else
            get => (_underlyingHandler.SslOptions.RemoteCertificateValidationCallback?.Target as ConnectHelper.CertificateCallbackMapper)?.FromHttpClientHandler;
            set
            {
                ThrowForModifiedManagedSslOptionsIfStarted();
                _underlyingHandler.SslOptions.RemoteCertificateValidationCallback = value != null ?
                    new ConnectHelper.CertificateCallbackMapper(value).ForSocketsHttpHandler :
                    null;
            }
#endif
        }

        public bool CheckCertificateRevocationList
        {
            get => _underlyingHandler.SslOptions.CertificateRevocationCheckMode == X509RevocationMode.Online;
            set
            {
                ThrowForModifiedManagedSslOptionsIfStarted();
                _underlyingHandler.SslOptions.CertificateRevocationCheckMode = value ? X509RevocationMode.Online : X509RevocationMode.NoCheck;
            }
        }

        public SslProtocols SslProtocols
        {
            get => _underlyingHandler.SslOptions.EnabledSslProtocols;
            set
            {
                ThrowForModifiedManagedSslOptionsIfStarted();
                _underlyingHandler.SslOptions.EnabledSslProtocols = value;
            }
        }

        public IDictionary<string, object?> Properties => _underlyingHandler.Properties;

        protected internal override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return DiagnosticsHandler.IsEnabled() ?
                _diagnosticsHandler.SendAsync(request, cancellationToken) :
                _underlyingHandler.SendAsync(request, cancellationToken);
        }

        // lazy-load the validator func so it can be trimmed by the ILLinker if it isn't used.
        private static Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>? s_dangerousAcceptAnyServerCertificateValidator;
        public static Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> DangerousAcceptAnyServerCertificateValidator =>
            Volatile.Read(ref s_dangerousAcceptAnyServerCertificateValidator) ??
            Interlocked.CompareExchange(ref s_dangerousAcceptAnyServerCertificateValidator, delegate { return true; }, null) ??
            s_dangerousAcceptAnyServerCertificateValidator;

        private void ThrowForModifiedManagedSslOptionsIfStarted()
        {
            // Hack to trigger an InvalidOperationException if a property that's stored on
            // SslOptions is changed, since SslOptions itself does not do any such checks.
            _underlyingHandler.SslOptions = _underlyingHandler.SslOptions;
        }
    }
}
