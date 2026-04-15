using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;

namespace YomiYa.Core.Services;

public class GoogleDriveSyncService
{
    private static readonly string[] Scopes = { DriveService.Scope.DriveAppdata };
    private const string ApplicationName = "YomiYa";
    private const string BinaryMime = "application/octet-stream";

    private DriveService? _driveService;

    public bool IsAuthenticated => _driveService != null;

    // =========================
    // AUTH
    // =========================
    public async Task<bool> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "credentials.json");

            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "No se encontró credentials.json. Debes agregarlo desde Google Cloud Console.",
                    path
                );

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);

            var secrets = await GoogleClientSecrets.FromStreamAsync(stream, cancellationToken);

            if (secrets?.Secrets == null)
                throw new Exception("El archivo credentials.json es inválido o está vacío.");

            string credPath = Path.Combine(AppContext.BaseDirectory, "token");

            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets.Secrets,
                Scopes,
                "user",
                cancellationToken,
                new FileDataStore(credPath, true)
            );

            _driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            Console.WriteLine("[GoogleDriveSync] Autenticación exitosa");
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[GoogleDriveSync] Auth Error: {e.Message}");
            return false;
        }
    }

    // =========================
    // CHECK AUTH (REAL)
    // =========================
    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            if (_driveService == null)
                return false;

            var aboutRequest = _driveService.About.Get();
            aboutRequest.Fields = "user";

            var result = await aboutRequest.ExecuteAsync();
            return result != null;
        }
        catch
        {
            return false;
        }
    }

    // =========================
    // GET BACKUP
    // =========================
    public async Task<Google.Apis.Drive.v3.Data.File?> GetLatestBackupAsync(string fileName)
    {
        if (_driveService == null)
            throw new InvalidOperationException("Not authenticated");

        var request = _driveService.Files.List();
        request.Spaces = "appDataFolder";
        request.Q = $"name = '{fileName}' and trashed = false";
        request.Fields = "files(id,name,modifiedTime)";

        var result = await request.ExecuteAsync();

        return result.Files?
            .OrderByDescending(x => x.ModifiedTimeDateTimeOffset ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
    }

    // =========================
    // DOWNLOAD
    // =========================
    public async Task DownloadFileAsync(string fileId, string destinationPath)
    {
        if (_driveService == null)
            throw new InvalidOperationException("Not authenticated");

        try
        {
            var request = _driveService.Files.Get(fileId);

            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);

            await request.DownloadAsync(fileStream);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[GoogleDriveSync] Download Error: {e}");
            throw;
        }
    }

    // =========================
    // UPLOAD / UPDATE
    // =========================
    public async Task UploadOrUpdateBackupAsync(string localFilePath, string fileName)
    {
        if (_driveService == null)
            throw new InvalidOperationException("Not authenticated");

        if (!File.Exists(localFilePath))
            throw new FileNotFoundException("Local file not found", localFilePath);

        try
        {
            var existingBackup = await GetLatestBackupAsync(fileName);

            var fileMetaData = new Google.Apis.Drive.v3.Data.File()
            {
                Name = fileName,
                Parents = new[] { "appDataFolder" }
            };

            await using var stream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);

            if (existingBackup != null)
            {
                var localModified = File.GetLastWriteTimeUtc(localFilePath);

                if (existingBackup.ModifiedTimeDateTimeOffset > localModified)
                {
                    Console.WriteLine("[GoogleDriveSync] WARNING: La versión en la nube es más reciente.");
                }

                var request = _driveService.Files.Update(
                    fileMetaData,
                    existingBackup.Id,
                    stream,
                    BinaryMime
                );

                request.ProgressChanged += progress =>
                {
                    Console.WriteLine($"[Upload] {progress.Status} - {progress.BytesSent} bytes");
                };

                var result = await request.UploadAsync();

                if (result.Status != UploadStatus.Completed)
                    throw new Exception($"Upload failed: {result.Status}");
            }
            else
            {
                var request = _driveService.Files.Create(fileMetaData, stream, BinaryMime);

                request.ProgressChanged += progress =>
                {
                    Console.WriteLine($"[Upload] {progress.Status} - {progress.BytesSent} bytes");
                };

                var result = await request.UploadAsync();

                if (result.Status != UploadStatus.Completed)
                    throw new Exception($"Upload failed: {result.Status}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[GoogleDriveSync] Upload Error: {e}");
            throw;
        }
    }

    // =========================
    // LOGOUT
    // =========================
    public Task LogoutAsync()
    {
        var credPath = Path.Combine(AppContext.BaseDirectory, "token.json");

        try
        {
            if (Directory.Exists(credPath))
                Directory.Delete(credPath, true);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[GoogleDriveSync] Logout cleanup error: {e}");
        }

        _driveService?.Dispose();
        _driveService = null;

        return Task.CompletedTask;
    }
}