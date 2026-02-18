using System.Diagnostics;
using System.IO;

namespace ScavKRInstaller;

public static class AutoInstaller
{
    private const string AutoConnectIpPort = "26.35.34.177:7790";
    private const string AutoConnectPlayerName = "grosFemboyFurry";
    private const string AutoConnectHostName = "furry";
    private const string AutoConnectPassword = "123";
    private const string InstallFolderName = "scavMULTI";
    private static readonly (string FolderPrefix, string DllName, string ResourceName)[] ExtraLocalMods =
    [
        ("ItemSpawnerIMGUI", "CU_ItemSpawner_Mod.dll", "EmbeddedMods.CU_ItemSpawner_Mod.dll"),
        ("SkipLore", "CU_Skip_Lore_Mod.dll", "EmbeddedMods.CU_Skip_Lore_Mod.dll"),
        ("QoL Unknown", "QoL Unknown.dll", "EmbeddedMods.QoL_Unknown.dll"),
    ];

    public static async Task<int> RunAsync()
    {
        LogHandler.Instance.Write("BEGIN: Starting headless automatic installation");

        Installer.HeadlessMode = true;
        Installer.InDownloadMode = true;
        Installer.ChangeSkinArchivePath = "";
        Installer.BepinZipArchivePath = "";
        Installer.ModZipArchivePath = "";

        FileOperations.DiscoverFilenames();

        if (FileOperations.CheckIfSaveFilesPresent(out string[] saveFilePaths))
        {
            Installer.SaveFilePaths = saveFilePaths;
        }
        else
        {
            Installer.SaveFilePaths = [];
        }

        string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        string installPath = Path.Combine(downloadsPath, InstallFolderName);
        Directory.CreateDirectory(installPath);
        Installer.GameFolderPath = installPath;

        string providedPath = Installer.GameFolderPath;
        try
        {
            FileOperations.HandleProvidedGamePath(ref providedPath);
        }
        catch (Exception ex)
        {
            LogHandler.Instance.Write($"CANCEL: invalid path | {ex}");
            return 1;
        }

        List<string> finalUnzipPaths = new();

        try
        {
            finalUnzipPaths.Add(await FileOperations.TryGameDownload(Constants.GameDownloadURLs));
        }
        catch (Exception ex)
        {
            LogHandler.Instance.Write($"CANCEL: game download failed | {ex}");
            return 1;
        }

        if (FileOperations.CheckForMod(Installer.GameFolderPath))
        {
            LogHandler.Instance.Write("INFO: Multiplayer mod already detected; automatic update will continue.");
        }

        try
        {
            Installer.ChangeSkinArchivePath = await FileOperations.DownloadArchive(Constants.ChangeSkinURL);
        }
        catch (Exception ex)
        {
            LogHandler.Instance.Write($"SKIP: ChangeSkin download failed | {ex}");
        }

        FileOperations.DeleteSavefiles(Installer.SaveFilePaths);

        if (!FileOperations.CheckForBepin(Installer.GameFolderPath))
        {
            try
            {
                Installer.BepinZipArchivePath = await FileOperations.DownloadArchive(Constants.BepinZipURL);
            }
            catch (Exception ex)
            {
                LogHandler.Instance.Write($"CANCEL: BepInEx download failed | {ex}");
                return 1;
            }
        }

        try
        {
            Installer.ModZipArchivePath = await FileOperations.DownloadArchive(Constants.ModZipURL);
        }
        catch (Exception ex)
        {
            LogHandler.Instance.Write($"CANCEL: Multiplayer mod download failed | {ex}");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(Installer.BepinZipArchivePath))
        {
            finalUnzipPaths.Add(Installer.BepinZipArchivePath);
        }
        if (!string.IsNullOrWhiteSpace(Installer.ChangeSkinArchivePath))
        {
            finalUnzipPaths.Add(Installer.ChangeSkinArchivePath);
        }
        finalUnzipPaths.Add(Installer.ModZipArchivePath);

        if (!FileOperations.UnzipFiles(finalUnzipPaths.ToArray(), out string[] unpackedDirs))
        {
            LogHandler.Instance.Write("CANCEL: archive extraction failed");
            return 1;
        }

        CloseRunningGameProcesses(Installer.GameFolderPath);

        if (!FileOperations.HandleCopyingFiles(unpackedDirs))
        {
            LogHandler.Instance.Write("CANCEL: file copy failed");
            return 1;
        }

        if (ModTextPatcher.TryPatchKrokoshaMod(
                Installer.GameFolderPath,
                AutoConnectIpPort,
                AutoConnectPlayerName,
                AutoConnectPassword,
                out string patchMessage
            ))
        {
            LogHandler.Instance.Write($"INFO: {patchMessage}");
        }
        else
        {
            LogHandler.Instance.Write($"WARN: {patchMessage}");
        }

        WriteConsoleQuickCommandsFile(Installer.GameFolderPath);
        InstallExtraLocalModsFromDownloads(downloadsPath, Installer.GameFolderPath);

        string vpnInstallResult = await VpnInstaller.TryInstallOpenSourceVpnAsync();
        LogHandler.Instance.Write($"INFO: {vpnInstallResult}");
        string vpnInfoResult = VpnInstaller.WriteGuestVpnInfoFile(Installer.GameFolderPath);
        LogHandler.Instance.Write($"INFO: {vpnInfoResult}");
        string vpnRuntimeResult = VpnInstaller.EnsureGuestVpnRuntimeFiles(Installer.GameFolderPath);
        LogHandler.Instance.Write($"INFO: {vpnRuntimeResult}");

        LogHandler.Instance.Write("DONE: Automatic installation successful.");

        if (!string.IsNullOrWhiteSpace(Installer.GamePath) && File.Exists(Installer.GamePath))
        {
            CreateAutoConnectLauncherFile();
            CreateDesktopShortcut();
            LaunchGameWithAutoConnect();
            LogHandler.Instance.Write($"INFO: Game launched with auto-connect from {Installer.GamePath}");
        }
        else
        {
            LogHandler.Instance.Write("WARN: Game executable not found after install; launch skipped.");
        }

        FileOperations.DeleteTempFiles();
        return 0;
    }

    private static void CreateAutoConnectLauncherFile()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Installer.GameFolderPath) || string.IsNullOrWhiteSpace(Installer.GamePath))
            {
                return;
            }

            string launcherPath = Path.Combine(Installer.GameFolderPath, "Launch_AutoConnect.bat");
            string launcherPs1Path = Path.Combine(Installer.GameFolderPath, "Launch_AutoConnect.ps1");
            string gameExeName = Path.GetFileName(Installer.GamePath);
            string contentPs1 = $$"""
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName Microsoft.VisualBasic
[System.Windows.Forms.Application]::EnableVisualStyles()

$root = $PSScriptRoot
$gameExe = $null
$hostPlayerName = '{{AutoConnectHostName}}'
$guestPlayerName = '{{AutoConnectPlayerName}}'
$defaultGuestIpPort = '{{AutoConnectIpPort}}'
$defaultGuestIp = $defaultGuestIpPort.Split(':')[0]
$openVpnGui = Join-Path $env:ProgramFiles 'OpenVPN\bin\openvpn-gui.exe'
$openVpnExe = Join-Path $env:ProgramFiles 'OpenVPN\bin\openvpn.exe'
$vpnConfig = Join-Path $root 'vpn\guest.ovpn'
$vpnAuth = Join-Path $root 'vpn\credentials.txt'
$guiStartedByLauncher = $false
$vpnProcess = $null
$logPath = Join-Path $root 'Launch_AutoConnect.log'

function Write-LaunchLog {
    param([string]$Message)
    try {
        Add-Content -Path $logPath -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | $Message"
    }
    catch {}
}

function Stop-VpnProcesses {
    if (($vpnProcess -ne $null) -and (-not $vpnProcess.HasExited)) {
        Stop-Process -Id $vpnProcess.Id -Force
    }
    if ($guiStartedByLauncher) {
        Get-Process openvpn-gui -ErrorAction SilentlyContinue | Stop-Process -Force
    }
}

function Show-LauncherError {
    param([string]$Message)
    [System.Windows.Forms.MessageBox]::Show(
        $Message,
        "Erreur lancement",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error
    ) | Out-Null
}

function Resolve-GameExePath {
    param(
        [string]$BasePath,
        [string]$ExpectedName
    )
    $direct = Join-Path $BasePath $ExpectedName
    if (Test-Path $direct) {
        return $direct
    }
    $found = Get-ChildItem -Path $BasePath -Filter $ExpectedName -File -Recurse | Select-Object -First 1
    if ($found) {
        return $found.FullName
    }
    return $null
}

function Get-PreferredIPv4 {
    $ips = [System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName()) |
        Where-Object { $_.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork -and -not $_.ToString().StartsWith('127.') } |
        ForEach-Object { $_.ToString() }
    $vpnIp = $ips | Where-Object { $_.StartsWith('26.') } | Select-Object -First 1
    if ($vpnIp) { return $vpnIp }
    return ($ips | Select-Object -First 1)
}

$gameExe = Resolve-GameExePath -BasePath $root -ExpectedName '{{gameExeName}}'
if ([string]::IsNullOrWhiteSpace($gameExe)) {
    Show-LauncherError "Impossible de trouver CasualtiesUnknown.exe.`nVérifiez les fichiers installés dans: $root"
    Write-LaunchLog "Game executable not found under $root."
    exit 1
}
$gameWorkingDir = Split-Path -Path $gameExe -Parent
Write-LaunchLog "Resolved game executable: $gameExe"

try {
    if (Test-Path $openVpnGui) {
        if (-not (Get-Process openvpn-gui -ErrorAction SilentlyContinue)) {
            Start-Process -FilePath $openVpnGui -WindowStyle Minimized | Out-Null
            $guiStartedByLauncher = $true
            Write-LaunchLog "OpenVPN GUI started."
        }
    }
    if ((Test-Path $openVpnExe) -and (Test-Path $vpnConfig)) {
        $vpnArgs = @('--config', $vpnConfig)
        if (Test-Path $vpnAuth) { $vpnArgs += @('--auth-user-pass', $vpnAuth) }
        $vpnProcess = Start-Process -FilePath $openVpnExe -ArgumentList $vpnArgs -WorkingDirectory $root -PassThru -WindowStyle Hidden
        Write-LaunchLog "OpenVPN tunnel process started."
    }
}
catch {
    Write-LaunchLog "VPN start warning: $($_.Exception.Message)"
}

$choice = [System.Windows.Forms.MessageBox]::Show(
    "Voulez-vous etre Host ?`nOui = Host`nNon = Guest",
    "Mode de connexion",
    [System.Windows.Forms.MessageBoxButtons]::YesNoCancel,
    [System.Windows.Forms.MessageBoxIcon]::Question
)

if ($choice -eq [System.Windows.Forms.DialogResult]::Cancel) {
    Stop-VpnProcesses
    Write-LaunchLog "User cancelled mode selection."
    exit 0
}

if ($choice -eq [System.Windows.Forms.DialogResult]::Yes) {
    $serverIp = Get-PreferredIPv4
    if ([string]::IsNullOrWhiteSpace($serverIp)) {
        $serverIp = $defaultGuestIp
    }
    $clipboardStatus = "IP copié dans le presse-papiers."
    try {
        $serverIp | clip
    }
    catch {
        $clipboardStatus = "Impossible de copier automatiquement l'IP."
    }
    [System.Windows.Forms.MessageBox]::Show(
        "Attention, notez bien l'ip du serveur: $serverIp`n$clipboardStatus`nDonnez-la aux guests.",
        "Informations Host",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Warning
    ) | Out-Null
    $ipPort = "${serverIp}:7790"
    $gameArgs = @('--ksmulti-starthost', '--ksmulti-ip-port', $ipPort, '--ksmulti-setname', $hostPlayerName)
}
else {
    $serverIpInput = [Microsoft.VisualBasic.Interaction]::InputBox(
        "Entrez ici l'ip du serveur",
        "Connexion Guest",
        $defaultGuestIp
    )
    if ([string]::IsNullOrWhiteSpace($serverIpInput)) {
        Stop-VpnProcesses
        Write-LaunchLog "Guest cancelled server IP input."
        exit 0
    }
    if ($serverIpInput.Contains(':')) {
        $ipPort = $serverIpInput
    }
    else {
        $ipPort = "${serverIpInput}:7790"
    }
    $gameArgs = @('--ksmulti-startclient', '--ksmulti-ip-port', $ipPort, '--ksmulti-setname', $guestPlayerName)
}

try {
    Write-LaunchLog "Starting game with args: $($gameArgs -join ' ')"
    $gameProcess = Start-Process -FilePath $gameExe -ArgumentList $gameArgs -WorkingDirectory $gameWorkingDir -PassThru -ErrorAction Stop
}
catch {
    Show-LauncherError "Le jeu n'a pas pu démarrer.`nChemin: $gameExe`nErreur: $($_.Exception.Message)"
    Write-LaunchLog "Failed to start game: $($_.Exception)"
    Stop-VpnProcesses
    exit 1
}

$gameProcess.WaitForExit()
Write-LaunchLog "Game exited with code: $($gameProcess.ExitCode)"
Stop-VpnProcesses
Write-LaunchLog "Launcher finished."
""";

            string contentBat = "@echo off\r\n" +
                                "cd /d \"%~dp0\"\r\n" +
                                "powershell -NoProfile -ExecutionPolicy Bypass -STA -File \"%~dp0Launch_AutoConnect.ps1\"\r\n";

            File.WriteAllText(launcherPs1Path, contentPs1);
            File.WriteAllText(launcherPath, contentBat);
            LogHandler.Instance.Write($"INFO: Created auto-connect launcher: {launcherPath}");
        }
        catch (Exception ex)
        {
            LogHandler.Instance.Write($"WARN: Failed to create auto-connect launcher file | {ex}");
        }
    }

    private static void LaunchGameWithAutoConnect()
    {
        string launcherPath = Path.Combine(Installer.GameFolderPath, "Launch_AutoConnect.bat");
        ProcessStartInfo psi = new ProcessStartInfo(launcherPath)
        {
            WorkingDirectory = Installer.GameFolderPath,
            UseShellExecute = true
        };
        Process.Start(psi);
    }

    private static void CreateDesktopShortcut()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Installer.GamePath) || !File.Exists(Installer.GamePath))
            {
                return;
            }

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = Path.Combine(desktopPath, "scavMULTI.lnk");

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                LogHandler.Instance.Write("WARN: WScript.Shell not available, desktop shortcut creation skipped.");
                return;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            string launcherPath = Path.Combine(Installer.GameFolderPath, "Launch_AutoConnect.bat");
            shortcut.TargetPath = launcherPath;
            shortcut.WorkingDirectory = Installer.GameFolderPath;
            shortcut.Arguments = "";
            shortcut.Description = "scavMULTI guest quick connect";
            shortcut.IconLocation = Installer.GamePath + ",0";
            shortcut.Save();

            LogHandler.Instance.Write($"INFO: Desktop shortcut created: {shortcutPath}");
        }
        catch (Exception ex)
        {
            LogHandler.Instance.Write($"WARN: Failed to create desktop shortcut | {ex}");
        }
    }

    private static void CloseRunningGameProcesses(string gameFolderPath)
    {
        if (string.IsNullOrWhiteSpace(gameFolderPath))
        {
            return;
        }

        string[] processNames = ["CasualtiesUnknown", "UnityCrashHandler64"];
        foreach (string name in processNames)
        {
            foreach (Process process in Process.GetProcessesByName(name))
            {
                try
                {
                    string? processPath = null;
                    try
                    {
                        processPath = process.MainModule?.FileName;
                    }
                    catch
                    {
                        // Access can fail on protected processes; fall back to process name.
                    }

                    if (!string.IsNullOrWhiteSpace(processPath) &&
                        !processPath.StartsWith(gameFolderPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    LogHandler.Instance.Write($"INFO: Closing running process before file update: {process.ProcessName} ({process.Id})");
                    process.Kill(true);
                    process.WaitForExit(15000);
                }
                catch (Exception ex)
                {
                    LogHandler.Instance.Write($"WARN: Failed to close process {process.ProcessName} ({process.Id}) | {ex.Message}");
                }
            }
        }
    }

    private static void InstallExtraLocalModsFromDownloads(string downloadsPath, string gameFolderPath)
    {
        try
        {
            string pluginsPath = Path.Combine(gameFolderPath, "BepInEx", "plugins");
            Directory.CreateDirectory(pluginsPath);

            foreach ((string folderPrefix, string dllName, string resourceName) in ExtraLocalMods)
            {
                string? sourcePath = FindLatestDownloadsModDll(downloadsPath, folderPrefix, dllName);
                string destinationPath = Path.Combine(pluginsPath, dllName);

                if (!string.IsNullOrWhiteSpace(sourcePath))
                {
                    if (!CopyFileWithRetry(sourcePath, destinationPath))
                    {
                        LogHandler.Instance.Write($"WARN: Failed to copy optional local mod after retries: {dllName}");
                        continue;
                    }

                    LogHandler.Instance.Write($"INFO: Installed optional local mod from Downloads: {dllName} (source: {sourcePath})");
                    continue;
                }

                if (TryInstallEmbeddedMod(resourceName, destinationPath))
                {
                    LogHandler.Instance.Write($"INFO: Installed optional embedded mod: {dllName}");
                }
                else
                {
                    LogHandler.Instance.Write($"WARN: Optional mod not found locally or in embedded resources: {folderPrefix}\\{dllName}");
                }
            }
        }
        catch (Exception ex)
        {
            LogHandler.Instance.Write($"WARN: Optional local mods install step failed | {ex.Message}");
        }
    }

    private static string? FindLatestDownloadsModDll(string downloadsPath, string folderPrefix, string dllName)
    {
        try
        {
            return Directory
                .EnumerateFiles(downloadsPath, dllName, SearchOption.AllDirectories)
                .Where(path =>
                {
                    string? dir = Path.GetDirectoryName(path);
                    return !string.IsNullOrWhiteSpace(dir) &&
                           dir.Contains(folderPrefix, StringComparison.OrdinalIgnoreCase);
                })
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            LogHandler.Instance.Write($"WARN: Search failed for optional local mod {dllName} | {ex.Message}");
            return null;
        }
    }

    private static bool CopyFileWithRetry(string sourceFile, string destinationFile, int attempts = 10, int delayMs = 500)
    {
        for (int i = 1; i <= attempts; i++)
        {
            try
            {
                File.Copy(sourceFile, destinationFile, true);
                return true;
            }
            catch (IOException ex) when (i < attempts)
            {
                LogHandler.Instance.Write($"WARN: Local mod copy locked ({i}/{attempts}) on {destinationFile} | {ex.Message}");
                Thread.Sleep(delayMs);
            }
            catch (UnauthorizedAccessException ex) when (i < attempts)
            {
                LogHandler.Instance.Write($"WARN: Local mod copy access denied ({i}/{attempts}) on {destinationFile} | {ex.Message}");
                Thread.Sleep(delayMs);
            }
        }

        return false;
    }

    private static bool TryInstallEmbeddedMod(string resourceName, string destinationPath)
    {
        try
        {
            using Stream? resourceStream = typeof(AutoInstaller).Assembly.GetManifestResourceStream(resourceName);
            if (resourceStream == null)
            {
                return false;
            }

            using FileStream output = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            resourceStream.CopyTo(output);
            return true;
        }
        catch (Exception ex)
        {
            LogHandler.Instance.Write($"WARN: Failed to install embedded mod resource {resourceName} | {ex.Message}");
            return false;
        }
    }

    private static void WriteConsoleQuickCommandsFile(string gameFolderPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(gameFolderPath))
            {
                return;
            }

            string helpPath = Path.Combine(gameFolderPath, "CONSOLE_QUICK_COMMANDS.txt");
            string content =
                "KROKOSHA MP - QUICK COMMANDS\r\n" +
                "============================\r\n" +
                "krok help\r\n" +
                "krok rules\r\n" +
                "krok rule [rule] [value]\r\n" +
                "krok maxplayers [number]\r\n" +
                "krok kick [player]\r\n\r\n" +
                "Useful examples:\r\n" +
                "krok rule FriendlyFire true\r\n" +
                "krok rule ForNextLayerAllMustReachEnd false\r\n";

            File.WriteAllText(helpPath, content);
            LogHandler.Instance.Write($"INFO: Created console quick command file: {helpPath}");
        }
        catch (Exception ex)
        {
            LogHandler.Instance.Write($"WARN: Failed to create CONSOLE_QUICK_COMMANDS.txt | {ex.Message}");
        }
    }
}
