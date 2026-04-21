using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using File = Google.Apis.Drive.v3.Data.File;

namespace YomiYa.Core.Services;

public class GoogleDriveSyncService
{
    private const string ApplicationName = "YomiYa";
    private const string BinaryMime = "application/octet-stream";
    private static readonly string[] Scopes = { DriveService.Scope.DriveAppdata };

    private DriveService? _driveService;

    public GoogleDriveSyncService()
    {
        _ = TrySilentLoginAsync();
    }

    public bool IsAuthenticated => _driveService != null;

    private async Task TrySilentLoginAsync()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            await using var stream = assembly.GetManifestResourceStream("YomiYa.credentials.json");
            
            var secrets = await GoogleClientSecrets.FromStreamAsync(stream);
            if (secrets?.Secrets == null) return;

            var credPath = Path.Combine(AppContext.BaseDirectory, "token");
            var dataStore = new FileDataStore(credPath, true);
            var token = await dataStore.GetAsync<TokenResponse>("user");

            if (token == null) return;

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = secrets.Secrets,
                Scopes = Scopes,
                DataStore = dataStore
            });

            var credential = new UserCredential(flow, "user", token);
            var isTokenValid = await credential.RefreshTokenAsync(CancellationToken.None);

            if (isTokenValid)
            {
                _driveService = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName
                });
                Console.WriteLine("[GoogleDriveSync] Silent login successful");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[GoogleDriveSync] Silent login failed: {e.Message}");
        }
    }

    public async Task<bool> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            await using var stream = assembly.GetManifestResourceStream("YomiYa.credentials.json");

            if (stream == null)
                throw new Exception("No se pudo cargar credentials.json embebido.");

            var secrets = await GoogleClientSecrets.FromStreamAsync(stream, cancellationToken);

            if (secrets?.Secrets == null)
                throw new Exception("El archivo credentials.json es inválido o está vacío.");

            var credPath = Path.Combine(AppContext.BaseDirectory, "token");

            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets.Secrets,
                Scopes,
                "user",
                cancellationToken,
                new FileDataStore(credPath, true)
            );

            _driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName
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

    public async Task<File?> GetLatestBackupAsync(string fileName)
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

    public async Task UploadOrUpdateBackupAsync(string localFilePath, string fileName)
    {
        if (_driveService == null)
            throw new InvalidOperationException("Not authenticated");

        if (!System.IO.File.Exists(localFilePath))
            throw new FileNotFoundException("Local file not found", localFilePath);

        try
        {
            var existingBackup = await GetLatestBackupAsync(fileName);

            if (existingBackup != null) await _driveService.Files.Delete(existingBackup.Id).ExecuteAsync();

            var fileMetaData = new File
            {
                Name = fileName,
                Parents = new[] { "appDataFolder" }
            };

            await using var stream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var request = _driveService.Files.Create(fileMetaData, stream, BinaryMime);
            request.Fields = "id";

            var result = await request.UploadAsync();

            if (result.Status != UploadStatus.Completed)
                throw new Exception($"Upload failed: {result.Exception.Message}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[GoogleDriveSync] Upload Error: {e}");
            throw;
        }
    }

    public Task LogoutAsync()
    {
        var credPath = Path.Combine(AppContext.BaseDirectory, "token");

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