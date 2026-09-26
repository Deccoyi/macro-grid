using MacroGrid.Core.Devices;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Sessions;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>Editor endpoints for pairing (PIN and QR payload) and for the paired devices.</summary>
internal static class DeviceApi
{
    private sealed record AssignProfileRequest(string? ProfileId);
    private sealed record FollowWindowRequest(bool FollowActiveWindow);

    public static RouteGroupBuilder MapDeviceApi(this RouteGroupBuilder api)
    {
        // Pairing is open only while the editor's Pairing window keeps asking for the code (PairingService.Keep):
        // the window polls GET, and POST opens it with a fresh PIN. With the window closed no PIN is valid.
        api.MapGet("/pairing/pin", (PairingService pairing) => new { pin = pairing.Keep().Pin });

        api.MapPost("/pairing/pin/regenerate", (PairingService pairing, ILoggerFactory loggers) => new { pin = OpenPairing(pairing, loggers).Pin });

        api.MapGet("/pairing/qr", (PairingService pairing) => BuildPairingQr(pairing.Keep()));

        api.MapPost("/pairing/qr/regenerate", (PairingService pairing, ILoggerFactory loggers) => BuildPairingQr(OpenPairing(pairing, loggers)));

        api.MapGet("/devices", (DeviceStore devices) =>
            devices.All.Select(d => new { d.Id, d.Name, d.PairedAt, d.LastSeenAt, d.AssignedProfileId, d.FollowActiveWindow, d.AutoSwitchLocked }));

        api.MapDelete("/devices/{id}", (string id, DeviceStore devices, ILoggerFactory loggers) =>
        {
            var name = devices.All.FirstOrDefault(d => d.Id == id)?.Name;
            if (!devices.Revoke(id)) return Results.NotFound();
            SecurityLogger(loggers).LogInformation(SecurityEvents.DeviceRemoved, "Security: pairing of device {Name} ({Device}) removed", name ?? "?", id);
            return Results.NoContent();
        });

        api.MapPut("/devices/{id}/profile", async (string id, HttpRequest request, DeviceStore devices) =>
        {
            var (valid, body) = await ApiResults.ReadJsonAsync<AssignProfileRequest>(request);
            if (!valid) return Results.BadRequest();

            return devices.AssignProfile(id, body?.ProfileId) ? Results.NoContent() : Results.NotFound();
        });

        api.MapPut("/devices/{id}/follow-window", async (string id, HttpRequest request, DeviceStore devices, SessionRegistry sessions, AutoProfileSwitcher autoSwitcher) =>
        {
            var (valid, body) = await ApiResults.ReadJsonAsync<FollowWindowRequest>(request);
            if (!valid || body is null) return Results.BadRequest();

            if (!devices.SetFollowActiveWindow(id, body.FollowActiveWindow)) return Results.NotFound();

            // Applies to the live session too, not just future connections — re-evaluate against the
            // current foreground window right away if it was just turned on.
            var session = sessions.All.FirstOrDefault(s => s.DeviceId == id);
            if (session is not null && body.FollowActiveWindow)
                await autoSwitcher.ReevaluateAsync(session);

            return Results.NoContent();
        });

        return api;
    }

    private static PairingCode OpenPairing(PairingService pairing, ILoggerFactory loggers)
    {
        var code = pairing.Open();
        SecurityLogger(loggers).LogInformation(SecurityEvents.PairingOpened, "Security: pairing opened with a new PIN, valid until {ExpiresAt:HH:mm:ss}", code.ExpiresAt.ToLocalTime());
        return code;
    }

    private static ILogger SecurityLogger(ILoggerFactory loggers) => loggers.CreateLogger("MacroGrid.Security");

    /// <summary>
    /// The pairing QR's payload: a <c>macrogrid://pair</c> deep-link URI carrying the LAN host, port and
    /// current PIN, so a client can decode it without agreeing on a bespoke delimited format. Uses the first
    /// LAN address <see cref="NetworkInfo.GetLanAddresses"/> reports (gateway-having adapters first); if the
    /// host has no LAN adapter up, <c>host</c> comes back empty and the editor should show a warning instead
    /// of a QR code, since a QR with no reachable host is useless.
    /// </summary>
    private static object BuildPairingQr(PairingCode code)
    {
        var (pin, expiresAt) = code;
        var host = NetworkInfo.GetLanAddresses().FirstOrDefault()?.ToString() ?? "";
        var text = string.IsNullOrEmpty(host)
            ? ""
            : $"macrogrid://pair?host={Uri.EscapeDataString(host)}&port={ServerApp.Port}&pin={pin}";
        return new { text, host, port = ServerApp.Port, pin, expiresAt };
    }
}
