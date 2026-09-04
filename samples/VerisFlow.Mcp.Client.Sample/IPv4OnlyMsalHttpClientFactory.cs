using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace VerisFlow.Mcp.Client.Sample;

/// <summary>
/// Factory supplying an HttpClient configured to strictly resolve and establish IPv4 connections.
/// </summary>
public class IPv4OnlyMsalHttpClientFactory : IMsalHttpClientFactory
{
    private static readonly Lazy<HttpClient> SharedClient = new(() =>
    {
        var handler = new SocketsHttpHandler
        {
            // ConnectCallback enforces IPv4 connection establishment
            ConnectCallback = async (context, cancellationToken) =>
            {
                var entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, cancellationToken);
                var ipv4Address = entry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);

                if (ipv4Address == null)
                {
                    throw new SocketException((int)SocketError.HostNotFound);
                }

                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };

                try
                {
                    await socket.ConnectAsync(new IPEndPoint(ipv4Address, context.DnsEndPoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };

        return new HttpClient(handler);
    });

    /// <summary>
    /// Returns the shared IPv4-only HttpClient instance.
    /// </summary>
    /// <returns>Configured HttpClient instance.</returns>
    public HttpClient GetHttpClient() => SharedClient.Value;
}