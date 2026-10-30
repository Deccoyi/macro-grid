using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MacroGrid.Core.Plugins.Js;

/// <summary>Where an approved network permission points, judged from its name: this computer, the local network or the internet. The editor shows the same label (tests/shared/network-scope-cases.json).</summary>
public enum NetworkScope { Local, Lan, Internet }

/// <summary>What an address really is.</summary>
public enum AddressKind { Loopback, Private, Public, Never }

/// <summary>A connection that the network rules refused. The message is safe to show to the script.</summary>
public sealed class NetworkRefusedException(string message) : IOException(message);

/// <summary>What a JavaScript plugin's network calls need from the host: which server ports are Macro Grid's own, and (for tests) how a name is resolved.</summary>
public sealed record JsNetworkPolicy(IReadOnlyCollection<int> OwnPorts, Func<string, CancellationToken, Task<IPAddress[]>>? Resolver = null)
{
    public static readonly JsNetworkPolicy None = new([]);
}

/// <summary>
/// The rules for where a plugin's approved network call may really connect. The permission names a host; the rules look at the address the
/// name resolves to, so a name cannot be used to reach something the person did not approve.
/// 1. The address must be of the kind the name says (local name: loopback, local-network name: private, any other name: public).
/// 2. Macro Grid's own ports on this computer are never reachable, whatever was approved.
/// 3. A script cannot set the headers that decide where a request goes or how it is framed.
/// </summary>
public static class JsNetworkGuard
{
    public static NetworkScope ScopeOf(string host)
    {
        host = host.Trim('[', ']').ToLowerInvariant();
        if (host == "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal)) return NetworkScope.Local;
        if (IPAddress.TryParse(host, out var ip))
        {
            return KindOf(ip) switch
            {
                AddressKind.Loopback => NetworkScope.Local,
                AddressKind.Private => NetworkScope.Lan,
                _ => NetworkScope.Internet,
            };
        }
        // A single-label name ("nas") or a local-only suffix is resolved on the local network.
        if (!host.Contains('.') || host.EndsWith(".local", StringComparison.Ordinal) || host.EndsWith(".lan", StringComparison.Ordinal)
            || host.EndsWith(".home.arpa", StringComparison.Ordinal) || host.EndsWith(".internal", StringComparison.Ordinal)) return NetworkScope.Lan;
        return NetworkScope.Internet;
    }

    public static AddressKind KindOf(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.Broadcast) || address.Equals(IPAddress.None)) return AddressKind.Never;
        if (IPAddress.IsLoopback(address)) return AddressKind.Loopback;
        var b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            if (b[0] >= 224 || b[0] == 0) return AddressKind.Never; // multicast, reserved, "this network"
            if (b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254)
                || (b[0] == 100 && b[1] is >= 64 and <= 127)) return AddressKind.Private;
            return AddressKind.Public;
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (b[0] == 0xff) return AddressKind.Never; // multicast
            if ((b[0] & 0xfe) == 0xfc || (b[0] == 0xfe && (b[1] & 0xc0) == 0x80)) return AddressKind.Private; // fc00::/7, fe80::/10
            return AddressKind.Public;
        }
        return AddressKind.Never;
    }

    /// <summary>The addresses of <paramref name="resolved"/> a connection to <paramref name="host"/>:<paramref name="port"/> may use, in the resolver's order.</summary>
    public static IReadOnlyList<IPAddress> Allowed(string host, int port, IEnumerable<IPAddress> resolved, IReadOnlyCollection<int> ownPorts, Func<IPAddress, bool> isOwnAddress)
    {
        var scope = ScopeOf(host);
        var ownPort = ownPorts.Contains(port);
        var allowed = new List<IPAddress>();
        foreach (var address in resolved)
        {
            var kind = KindOf(address);
            var matches = scope switch
            {
                NetworkScope.Local => kind == AddressKind.Loopback,
                NetworkScope.Lan => kind == AddressKind.Private,
                _ => kind == AddressKind.Public,
            };
            if (!matches) continue;
            if (ownPort && (kind == AddressKind.Loopback || isOwnAddress(address))) continue;
            allowed.Add(address);
        }
        return allowed;
    }

    /// <summary>The headers a script cannot set: they decide where a request goes or how it is framed on the wire.</summary>
    public static bool IsRefusedHeader(string name)
    {
        var n = name.Trim().ToLowerInvariant();
        return n is "host" or "origin" or "connection" or "upgrade" or "content-length" or "transfer-encoding" or "te" or "trailer" or "expect"
            || n.StartsWith("sec-", StringComparison.Ordinal) || n.StartsWith("proxy-", StringComparison.Ordinal);
    }

    /// <summary>True for a header name or value that cannot be sent as it is (empty or non-token name, a control character or line break in either).</summary>
    public static bool IsMalformedHeader(string name, string value)
    {
        if (name.Length == 0) return true;
        foreach (var c in name)
            if (c <= ' ' || c >= 127 || "()<>@,;:\\\"/[]?={}".Contains(c)) return true;
        foreach (var c in value)
            if (c is '\r' or '\n' or '\0') return true;
        return false;
    }

    /// <summary>True when the address belongs to one of this computer's own network cards.</summary>
    public static bool IsOwnAddress(IPAddress address)
    {
        try
        {
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                foreach (var u in nic.GetIPProperties().UnicastAddresses)
                    if (u.Address.Equals(address)) return true;
        }
        catch (NetworkInformationException) { }
        return false;
    }

    /// <summary>
    /// Opens the socket for an approved request. The name is resolved here, the addresses the rules refuse are dropped, and the connection goes
    /// to an address that was checked, so a second, different answer from the name service cannot be used. <paramref name="onRefused"/> is told
    /// (host, port, reason) when nothing is left; the caller reports it.
    /// </summary>
    public static async ValueTask<Stream> ConnectAsync(DnsEndPoint endpoint, JsNetworkPolicy policy, Func<IPAddress, bool> isOwnAddress, Action<string, int, string> onRefused, CancellationToken ct)
    {
        IPAddress[] resolved;
        if (IPAddress.TryParse(endpoint.Host.Trim('[', ']'), out var literal)) resolved = [literal];
        else
        {
            try { resolved = await (policy.Resolver ?? ((h, t) => Dns.GetHostAddressesAsync(h, t)))(endpoint.Host, ct); }
            catch (SocketException ex) { throw new NetworkRefusedException($"The name {endpoint.Host} could not be resolved: {ex.SocketErrorCode}"); }
        }

        var allowed = Allowed(endpoint.Host, endpoint.Port, resolved, policy.OwnPorts, isOwnAddress);
        if (allowed.Count == 0)
        {
            const string reason = "The address is not of the kind that was approved, or it is Macro Grid itself.";
            onRefused(endpoint.Host, endpoint.Port, reason);
            throw new NetworkRefusedException(reason);
        }

        SocketException? last = null;
        foreach (var address in allowed)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException ex) { socket.Dispose(); last = ex; }
            catch { socket.Dispose(); throw; }
        }
        throw new IOException($"Could not connect to {endpoint.Host}:{endpoint.Port}: {last?.SocketErrorCode}", last);
    }

    /// <summary>The handler a plugin's HTTP client uses: no redirects (a redirect could leave the approved host), no proxy (the rules need to see the real address), and the guarded connect.</summary>
    public static SocketsHttpHandler CreateHandler(JsNetworkPolicy policy, Action<string, int, string> onRefused) => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        ConnectCallback = (context, ct) => ConnectAsync(context.DnsEndPoint, policy, IsOwnAddress, onRefused, ct),
    };
}
