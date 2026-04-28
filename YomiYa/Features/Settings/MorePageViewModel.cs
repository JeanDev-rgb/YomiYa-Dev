using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using YomiYa.Core.Dialogs;
using YomiYa.Core.IO;
using YomiYa.Core.Localization;
using YomiYa.Core.Services;
using YomiYa.Core.Settings;
using YomiYa.Core.Theme;
using YomiYa.Domain.Enums;
using YomiYa.Utils;

namespace YomiYa.Features.Settings;

public partial class MorePageViewModel : ViewModelBase
{
    private const string BackupFileName = "yomiya_backup.zip";

    // Dependencias inyectadas
    private readonly IDialogService _dialogService;
    private readonly GoogleDriveSyncService _driveService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private bool _isSyncing;
    [ObservableProperty] private string _syncStatusMessage = LanguageHelper.GetText("WaitingForAction");

    #region Constructor

    // El contenedor de servicios inyectará automáticamente estas dependencias
    public MorePageViewModel(
        IDialogService dialogService,
        GoogleDriveSyncService driveService,
        ISettingsService settingsService)
    {
        _dialogService = dialogService;
        _driveService = driveService;
        _settingsService = settingsService;

        AvailableThemes = new ObservableCollection<string>(ThemeManager.AvailableThemes.Keys);

        // Se usa la instancia inyectada para obtener la configuración actual
        var savedThemePath = _settingsService.Settings.SelectedTheme;

        SelectedTheme = ThemeManager.AvailableThemes
            .FirstOrDefault(t => t.Value.FilePath == savedThemePath)
            .Key ?? "Oscuro";

        SelectedLanguage = _settingsService.Settings.SelectedLanguage;

        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SelectedLanguage))
            {
                LanguageHelper.SetLanguage(SelectedLanguage);
                _settingsService.Settings.SelectedLanguage = SelectedLanguage;
                _settingsService.Save();
            }
            else if (e.PropertyName == nameof(SelectedTheme) && !string.IsNullOrEmpty(SelectedTheme))
            {
                if (ThemeManager.AvailableThemes.TryGetValue(SelectedTheme, out var themeInfo))
                {
                    ThemeManager.ApplyTheme(themeInfo.FilePath);
                    _settingsService.Settings.SelectedTheme = themeInfo.FilePath;
                    _settingsService.Save();
                }
            }
        };

        UpdateLocalizedTexts();

        _ = CheckAuthStatusAsync();
    }

    #endregion

    #region Methods

    private async Task CheckAuthStatusAsync()
    {
        IsAuthenticated = await _driveService.IsAuthenticatedAsync();
        SyncStatusMessage = IsAuthenticated
            ? LanguageHelper.GetText("GoogleDriveConnected")
            : LanguageHelper.GetText("WaitingForAction");
    }

    protected override void UpdateLocalizedTexts()
    {
        AdditionalSettingsText = LanguageHelper.GetText("AdditionalSettings");
        ApplicationLanguageText = LanguageHelper.GetText("ApplicationLanguage");
        HostText = LanguageHelper.GetText("Host");
        ManualProxyConfigurationText = LanguageHelper.GetText("ManualProxyConfiguration");
        PasswordText = LanguageHelper.GetText("Password");
        PortText = LanguageHelper.GetText("Port");
        ResetText = LanguageHelper.GetText("Reset");
        SaveText = LanguageHelper.GetText("Save");
        UsernameOptionalText = LanguageHelper.GetText("UsernameOptional");
        CloudSynchronizationText = LanguageHelper.GetText("CloudSynchronization");
        SignInWithGoogleDriveText = LanguageHelper.GetText("SignInWithGoogleDrive");
        SignOutText = LanguageHelper.GetText("SignOut");
        WaitingForAction = LanguageHelper.GetText("WaitingForAction");
        ApplicationThemeText = LanguageHelper.GetText("ApplicationTheme");
        ImportThemeText = LanguageHelper.GetText("ImportTheme");
        UploadBackupText = LanguageHelper.GetText("UploadBackup");
        RestoreBackupText = LanguageHelper.GetText("RestoreBackup");
        RestartRequiredText = LanguageHelper.GetText("RestartRequired");

        if (IsSyncing)
            SyncStatusMessage = LanguageHelper.GetText("BrowserAuthPending");
        else if (IsAuthenticated)
            SyncStatusMessage = LanguageHelper.GetText("GoogleDriveConnected");
        else
            SyncStatusMessage = LanguageHelper.GetText("WaitingForAction");
    }

    #endregion

    #region Properties

    public ObservableCollection<string> AvailableThemes { get; }
    public ObservableCollection<Language> Languages { get; } = new(Enum.GetValues<Language>());

    [ObservableProperty] private string _selectedTheme = string.Empty;
    [ObservableProperty] private Language _selectedLanguage;

    [ObservableProperty] private string _proxyHost = string.Empty;
    [ObservableProperty] private int _proxyPort;
    [ObservableProperty] private string _proxyUsername = string.Empty;
    [ObservableProperty] private string _proxyPassword = string.Empty;

    [ObservableProperty] private string _additionalSettingsText = string.Empty;
    [ObservableProperty] private string _applicationLanguageText = string.Empty;
    [ObservableProperty] private string _hostText = string.Empty;
    [ObservableProperty] private string _manualProxyConfigurationText = string.Empty;
    [ObservableProperty] private string _passwordText = string.Empty;
    [ObservableProperty] private string _portText = string.Empty;
    [ObservableProperty] private string _resetText = string.Empty;
    [ObservableProperty] private string _saveText = string.Empty;
    [ObservableProperty] private string _usernameOptionalText = string.Empty;
    [ObservableProperty] private string _cloudSynchronizationText = string.Empty;
    [ObservableProperty] private string _signInWithGoogleDriveText = string.Empty;
    [ObservableProperty] private string _signOutText = string.Empty;
    [ObservableProperty] private string _waitingForAction = string.Empty;
    [ObservableProperty] private string _applicationThemeText = string.Empty;
    [ObservableProperty] private string _importThemeText = string.Empty;
    [ObservableProperty] private string _uploadBackupText = string.Empty;
    [ObservableProperty] private string _restoreBackupText = string.Empty;
    [ObservableProperty] private string _restartRequiredText = string.Empty;

    #endregion

    #region Commands

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        if (IsSyncing || !IsAuthenticated) return;

        IsSyncing = true;
        SyncStatusMessage = LanguageHelper.GetText("RestoringBackup");

        try
        {
            var backupFile = await _driveService.GetLatestBackupAsync(BackupFileName);
            if (backupFile == null)
            {
                SyncStatusMessage = LanguageHelper.GetText("NoBackupFound");
                return;
            }

            var tempZipPath = Path.Combine(Path.GetTempPath(), BackupFileName);
            await _driveService.DownloadFileAsync(backupFile.Id, tempZipPath);

            SqliteConnection.ClearAllPools();
            ZipFile.ExtractToDirectory(tempZipPath, AppContext.BaseDirectory, true);
            File.Delete(tempZipPath);

            SyncStatusMessage = LanguageHelper.GetText("RestoreSuccessful");
            await _dialogService.ShowRestartDialogAsync();
        }
        catch (Exception ex)
        {
            SyncStatusMessage = LanguageHelper.GetText("RestoreFailed");
            Console.WriteLine($"[Backup] Restore failed: {ex.Message}");
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private async Task LogoutGoogleAsync()
    {
        await _driveService.LogoutAsync();
        IsAuthenticated = false;
        SyncStatusMessage = LanguageHelper.GetText("WaitingForAction");
    }

    [RelayCommand]
    private async Task UploadBackupAsync()
    {
        if (IsSyncing || !IsAuthenticated) return;

        IsSyncing = true;
        SyncStatusMessage = LanguageHelper.GetText("UploadingBackup");

        try
        {
            var tempZipPath = Path.Combine(Path.GetTempPath(), BackupFileName);
            if (File.Exists(tempZipPath)) File.Delete(tempZipPath);

            SqliteConnection.ClearAllPools();

            using (var archive = ZipFile.Open(tempZipPath, ZipArchiveMode.Create))
            {
                var dbPath = PathHelper.GetDatabasePath();
                archive.CreateEntryFromFile(dbPath, Path.Combine("Data", Path.GetFileName(dbPath)));

                var settingsPath = Path.Combine(AppContext.BaseDirectory, "Data", "app-settings.json");
                if (File.Exists(settingsPath))
                    archive.CreateEntryFromFile(settingsPath, Path.Combine("Data", "app-settings.json"));

                var pluginsPath = PathHelper.PluginsPath;
                if (Directory.Exists(pluginsPath))
                    foreach (var file in Directory.GetFiles(pluginsPath, "*", SearchOption.AllDirectories))
                    {
                        var entryName = Path.GetRelativePath(pluginsPath, file);
                        archive.CreateEntryFromFile(file, Path.Combine("Plugins", entryName));
                    }
            }

            await _driveService.UploadOrUpdateBackupAsync(tempZipPath, BackupFileName);
            File.Delete(tempZipPath);

            SyncStatusMessage = LanguageHelper.GetText("UploadSuccessful");
        }
        catch (Exception ex)
        {
            SyncStatusMessage = LanguageHelper.GetText("UploadFailed");
            Console.WriteLine($"[Backup] Upload failed: {ex.Message}");
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private async Task ImportThemeAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is null)
            return;

        var previouslySelectedTheme = SelectedTheme;

        var filePickerOptions = new FilePickerOpenOptions
        {
            Title = "Importar Temas",
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("Archivos de Tema XML") { Patterns = ["*.xml"] }]
        };

        var selectedFiles = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(filePickerOptions);
        if (selectedFiles.Count == 0) return;

        foreach (var file in selectedFiles)
            try
            {
                var destinationPath = Path.Combine(ThemeManager.ExternalThemesDirectory, file.Name);
                await using var sourceStream = await file.OpenReadAsync();
                await using var destinationStream = File.Create(destinationPath);
                await sourceStream.CopyToAsync(destinationStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al importar el tema '{file.Name}': {ex.Message}");
            }

        ThemeManager.ReloadAvailableThemes();
        AvailableThemes.Clear();
        foreach (var themeName in ThemeManager.AvailableThemes.Keys) AvailableThemes.Add(themeName);

        if (!string.IsNullOrEmpty(previouslySelectedTheme) && AvailableThemes.Contains(previouslySelectedTheme))
            SelectedTheme = previouslySelectedTheme;
    }

    [RelayCommand]
    private void SaveProxy()
    {
        if (string.IsNullOrWhiteSpace(ProxyHost) || ProxyPort <= 0) return;

        var proxyUri = new Uri($"http://{ProxyHost}:{ProxyPort}");
        var proxy = new WebProxy(proxyUri);

        if (!string.IsNullOrWhiteSpace(ProxyUsername) && !string.IsNullOrWhiteSpace(ProxyPassword))
            proxy.Credentials = new NetworkCredential(ProxyUsername, ProxyPassword);

        GlobalProxy.Proxy = proxy;
    }

    [RelayCommand]
    private void ResetProxy()
    {
        ProxyHost = string.Empty;
        ProxyPort = 0;
        ProxyUsername = string.Empty;
        ProxyPassword = string.Empty;
        GlobalProxy.Proxy = null;
    }

    [RelayCommand]
    private async Task LoginGoogleAsync()
    {
        if (IsSyncing) return;

        IsSyncing = true;
        SyncStatusMessage = LanguageHelper.GetText("BrowserAuthPending");

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        try
        {
            IsAuthenticated = await _driveService.AuthenticateAsync(cts.Token);
            SyncStatusMessage = IsAuthenticated
                ? LanguageHelper.GetText("GoogleDriveConnected")
                : LanguageHelper.GetText("GoogleDriveLoginFailed");
        }
        catch (TaskCanceledException)
        {
            SyncStatusMessage = LanguageHelper.GetText("Timeout");
            IsAuthenticated = false;
        }
        finally
        {
            IsSyncing = false;
        }
    }

    #endregion
}