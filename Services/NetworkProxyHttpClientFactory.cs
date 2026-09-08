using System.Net;
using System.Net.Sockets;
using FxShell.Models;

namespace FxShell.Services;

/// <summary>
/// Creates HTTP clients that use the application's global proxy. The socket
/// callback keeps HTTP, SOCKS4 and SOCKS5 on the same proxy implementation used
/// by terminal connections.
/// </summary>
public static class NetworkProxyHttpClientFactory
{
    public static HttpClient Create(Func<ProxySettings?>? proxyProvider = null)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false
        };
        handler.ConnectCallback = async (context, cancellationToken) =>
        {
            var proxy = proxyProvider?.Invoke();
            var client = proxy?.IsEnabled == true
                ? await ProxyConnectionFactory.ConnectTcpAsync(
                    context.DnsEndPoint.Host,
                    context.DnsEndPoint.Port,
                    proxy,
                    cancellationToken).ConfigureAwait(false)
                : await ConnectDirectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);

            // The NetworkStream owns the socket returned by TcpClient. The
            // stream lifetime is managed by SocketsHttpHandler.
            return client.GetStream();
        };

        return new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    /// <summary>
    /// Velopack currently exposes HttpClientHandler customization rather than
    /// a socket callback. HTTP proxies can therefore be applied directly;
    /// SOCKS proxies are used by Agent and terminal traffic.
    /// </summary>
    public static IWebProxy? CreateHttpProxy(ProxySettings? proxy)
    {
        if (proxy?.IsEnabled != true || proxy.Protocol != ProxyProtocol.Http)
            return null;

        var uri = new UriBuilder(Uri.UriSchemeHttp, proxy.Host, proxy.Port).Uri;
        var webProxy = new WebProxy(uri)
        {
            BypassProxyOnLocal = false
        };
        var username = proxy.Username?.Trim();
        var password = PasswordEncryptionService.DecryptEncrypted(proxy.Password);
        if (!string.IsNullOrWhiteSpace(username))
            webProxy.Credentials = new NetworkCredential(username, password);
        return webProxy;
    }

    private static async Task<TcpClient> ConnectDirectAsync(
        DnsEndPoint endpoint,
        CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken)
                .ConfigureAwait(false);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
