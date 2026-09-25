using System.ComponentModel;
using System.Diagnostics;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Updates;
using Microsoft.Win32;

namespace MacroGrid.Host.Updates;

/// <summary>
/// "Install now": downloads the installer of the offered release, verifies it and starts it as a visible setup (Windows asks for administrator
/// permission; the setup shows the user agreement when its text changed). The app keeps running while the setup is open: if the person cancels it
/// or it fails, nothing has changed and the installed version carries on; once the setup starts replacing files it closes the app itself and
/// starts the new one. Only for a copy the installer put on the PC; a portable copy is sent to the release page instead. The work runs in the
/// background and its progress is read through <see cref="Status"/>.
/// </summary>
internal sealed class UpdateInstaller(UpdateService updates, InstallerDownloader downloader, IHostApplicationLifetime lifetime, ILogger<UpdateInstaller> log)
{
    /// <summary>The Inno Setup uninstall key of Macro Grid (its AppId plus "_is1"), which records where the installer put the app.</summary>
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{6B0B0F6E-5C2B-4D53-9E0A-3D4B7B7A6C11}_is1";

    /// <summary>Windows' answer when the person declines the administrator prompt (ERROR_CANCELLED).</summary>
    private const int ElevationDeclined = 1223;

    private static readonly string UpdatesRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacroGrid", "updates");

    private readonly object _lock = new();
    private UpdateSnapshot.InstallStatus _status = new("idle", 0, null);
    private bool _running;

    /// <summary>Raised when the setup reported success and this process is still alive; the tray then exits the app the normal way.</summary>
    public event Action? ExitRequested;

    public UpdateSnapshot.InstallStatus Status
    {
        get { lock (_lock) return _status; }
    }

    /// <summary>Whether the running copy was put here by the installer, so the installer can upgrade it in place.</summary>
    public bool IsInstalledByInstaller { get; } = DetectInstalled();

    /// <summary>Begins the download and install of the offered release. The reason is set when it cannot start.</summary>
    public bool TryStart(out string? refusal)
    {
        refusal = null;
        if (updates.Offer?.Latest is not { CanInstall: true } release)
        {
            refusal = "No installable update is available.";
            return false;
        }
        if (!IsInstalledByInstaller)
        {
            refusal = "This copy was not installed by the installer; use the release page.";
            return false;
        }

        lock (_lock)
        {
            if (_running)
            {
                refusal = "An update is already being installed.";
                return false;
            }
            _running = true;
            _status = new("downloading", 0, null);
        }

        _ = Task.Run(() => RunAsync(release, lifetime.ApplicationStopping));
        return true;
    }

    /// <summary>
    /// Keeps at most one downloaded installer (the newest that is newer than the running version) and deletes the rest, see
    /// <see cref="DownloadedInstallers"/>. Runs at every start, when a new download replaces an older one, and never fails the caller.
    /// </summary>
    public void CleanUpDownloads(ReleaseVersion? justDownloaded = null)
    {
        if (!ReleaseVersion.TryParse(ClientHub.ServerVersion, out var running)) return;
        try
        {
            DownloadedInstallers.CleanUp(UpdatesRoot, running, (path, ex) => log.LogWarning(ex, "Could not remove {Path} from the downloaded installers.", path), justDownloaded);
        }
        catch (Exception ex)
        {
            // Housekeeping only: whatever goes wrong here must not stop the app from starting or an update from running.
            log.LogWarning(ex, "Cleaning up the downloaded installers failed.");
        }
    }

    private async Task RunAsync(ReleaseInfo release, CancellationToken cancellationToken)
    {
        try
        {
            var progress = new Progress<double>(fraction => SetStatus(new("downloading", (int)Math.Round(fraction * 100), null)));
            var path = await downloader.DownloadAsync(release, UpdatesRoot, progress, cancellationToken);
            CleanUpDownloads(release.Version); // the other downloads this one replaces; never the one that was just fetched

            SetStatus(new("starting", 100, null));
            using var setup = StartInstaller(path);
            log.LogInformation("Started the installer for {Version}.", release.Version);

            // The setup closes this app when it begins replacing files, so getting past this line means it ended without installing.
            await setup.WaitForExitAsync(cancellationToken);
            var exitCode = TryGetExitCode(setup);
            log.LogInformation("The installer for {Version} ended with exit code {ExitCode} and the app is still running.", release.Version, exitCode);
            if (exitCode == 0)
            {
                ExitRequested?.Invoke();
                return;
            }
            Cancelled();
        }
        catch (UpdateDownloadException ex)
        {
            log.LogWarning(ex, "The installer for {Version} could not be downloaded.", release.Version);
            Fail(ex.Code switch
            {
                UpdateDownloadException.Refused => "refused",
                UpdateDownloadException.Verify => "verify",
                _ => "download",
            });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ElevationDeclined)
        {
            log.LogInformation("The administrator prompt for the installer was declined.");
            Fail("declined");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            log.LogWarning(ex, "The installer could not be started.");
            Fail("start");
        }
        catch (OperationCanceledException)
        {
            Fail("download");
        }
    }

    /// <summary>A visible setup that upgrades in place; <c>/UPDATE</c> tells its script to skip the pages it does not need and to start the app again when it is done.</summary>
    private static Process StartInstaller(string path) =>
        Process.Start(new ProcessStartInfo(path, InstallerArguments.ForUpdate)
        {
            UseShellExecute = true,
            Verb = "runas",
        }) ?? throw new InvalidOperationException("The installer did not start.");

    private static int? TryGetExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }

    private void SetStatus(UpdateSnapshot.InstallStatus status)
    {
        lock (_lock) _status = status;
    }

    /// <summary>The person cancelled the setup (or it stopped before installing): not an error, the installed version keeps running.</summary>
    private void Cancelled()
    {
        lock (_lock)
        {
            _status = new("cancelled", 0, null);
            _running = false;
        }
    }

    private void Fail(string error)
    {
        lock (_lock)
        {
            _status = new("failed", 0, error);
            _running = false;
        }
    }

    private static bool DetectInstalled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(UninstallKey);
            return InstallLocation.IsSameFolder(key?.GetValue("InstallLocation") as string, AppContext.BaseDirectory);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
