using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace ScavKRInstaller;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class Installer : Window
{
    public static string GamePath="";
    public static string GameFolderPath="";
    public static string[] SaveFilePaths=[];
    public static string ModZipArchivePath="";
    public static string BepinZipArchivePath="";
    public static readonly string ModZipURL=@"https://github.com/Krokosha666/cas-unk-krokosha-multiplayer-coop/archive/refs/heads/main.zip";
    public static readonly string BepinZipURL= @"https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.4/BepInEx_win_x64_5.4.23.4.zip";
    public Installer()
    {
        InitializeComponent();
        string[] saveFilePaths;
        if(FileOperations.CheckIfSaveFilesPresent(out saveFilePaths))
        {
            Installer.SaveFilePaths=saveFilePaths;
        }
        FileOperations.DeleteTempFiles();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute=true });
    }

    private void ButtonBrowsePath_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new OpenFileDialog();
        dialog.Filter="Game executable (.exe)|CasualtiesUnknown.exe";
        dialog.Multiselect=false;
        bool? result = dialog.ShowDialog();
        if(result==null) return;
        if((bool)result)
        {
            string gameFile = dialog.FileName;
            if (FileOperations.HandleProvidedGamePath(ref gameFile))
            {
                this.TextBoxGamePath.Text=gameFile;
            }
        }
    }

    private void TextBoxGamePath_TextChanged(object sender, TextChangedEventArgs e)
    {
        Installer.GamePath=this.TextBoxGamePath.Text;
        Installer.GameFolderPath=Installer.GamePath.Substring(0, Installer.GamePath.Length - (Installer.GamePath.Length - Installer.GamePath.LastIndexOf('\\')));
    }

    private void SetStatus(string status)
    {
        this.ButtonInstall.Content=status;
    }
    private async void ButtonInstall_Click(object sender, RoutedEventArgs e)
    {
        this.ButtonInstall.IsEnabled = false;
        var readyChecks=new Func<bool>[]
        {
            () => !String.IsNullOrEmpty(Installer.GamePath),
        };
        bool ready = readyChecks.All(x => x());
        if (!ready)
        {
            MessageBox.Show("Game path is invalid!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            goto CancelInstallation;
        }
        if (FileOperations.CheckForMod(Installer.GameFolderPath))
        {
            MessageBoxResult msgBoxModAlreadyInstalled = MessageBox.Show("Looks like the mod is already installed!\n\nInstaller is going to download and install the latest version of the mod from github.\n\nContinue?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if(msgBoxModAlreadyInstalled == MessageBoxResult.No) goto CancelInstallation;
        }
        if ((bool)this.CheckBoxSavefileDelete.IsChecked)
        {
            FileOperations.DeleteSavefiles(Installer.SaveFilePaths);
        }
        if(!FileOperations.CheckForBepin(Installer.GameFolderPath))
        {
            this.SetStatus("Downloading BepinEX...");
            try
            {
                Installer.BepinZipArchivePath=await FileOperations.DownloadArchive(Installer.BepinZipURL);
            }
            catch(TimeoutException)
            {
                goto CancelInstallation;
            }
        }
        this.SetStatus("Downloading multiplayer mod...");
        try
        {
            Installer.ModZipArchivePath=await FileOperations.DownloadArchive(Installer.ModZipURL);
        }
        catch(TimeoutException)
        {
            goto CancelInstallation;
        }
        List<string> finalUnzipPaths=new();
        if(!String.IsNullOrEmpty(Installer.BepinZipArchivePath))
        {
            finalUnzipPaths.Add(Installer.BepinZipArchivePath);
        }
        finalUnzipPaths.Add(Installer.ModZipArchivePath);
        this.SetStatus("Extracting archives...");
        string[] unpackedDirs = [];
        if (!FileOperations.UnzipFiles(finalUnzipPaths.ToArray(), out unpackedDirs))
        {
            MessageBox.Show("Error while unzipping mods! Ensure that your %TEMP% folder has write permissions!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            goto CancelInstallation;
        }
        this.SetStatus("Moving files...");
        if (!FileOperations.HandleCopyingFiles(unpackedDirs))
        {
            MessageBox.Show("Error while copying files to the game folder! Ensure that your game folder has write permissions!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            goto CancelInstallation;
        }
        this.SetStatus("Done!");
        MessageBoxResult msgBoxFinished = MessageBox.Show("Mod has been succesfully installed! Don't forget to delete this installer.\n\nLaunch the game?", "Message", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if(msgBoxFinished == MessageBoxResult.Yes) Process.Start(new ProcessStartInfo(Installer.GamePath));
        Environment.Exit(0);
        CancelInstallation:
        {
            this.ButtonInstall.IsEnabled = true;
            this.ButtonInstall.Content = "Install";
            FileOperations.DeleteTempFiles();
            return;
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        FileOperations.DeleteTempFiles();
    }
}