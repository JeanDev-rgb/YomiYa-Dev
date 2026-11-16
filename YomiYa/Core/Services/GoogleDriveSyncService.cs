using System;
using System.Collections.Generic;
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

    public bool IsAuthenticated => _driveService is not null;

    private async Task TrySilentLoginAsync()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            await using var stream = assembly.GetManifestResourceStream("YomiYa.credentials.json");

            var secrets = await GoogleClientSecrets.FromStreamAsync(stream);
            if (secrets?.Secrets is null) return;

            var credPath = Path.Combine(AppContext.BaseDirectory, "token");
            var dataStore = new FileDataStore(credPath, true);
            var token = await dataStore.GetAsync<TokenResponse>("user");

            if (token is null) return;

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

            if (stream is null)
                throw new Exception("No se pudo cargar credentials.json embebido.");

            var secrets = await GoogleClientSecrets.FromStreamAsync(stream, cancellationToken);

            if (secrets?.Secrets is null)
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
            if (_driveService is null)
                return false;

            var aboutRequest = _driveService.About.Get();
            aboutRequest.Fields = "user";

            var result = await aboutRequest.ExecuteAsync();
            return result is not null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<File?> GetLatestBackupAsync(string fileName)
    {
        if (_driveService is null)
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
        if (_driveService is null)
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

    public async Task UploadOrUpdateBackupAsync(
        string localFilePath,
        string baseFileName,
        int maxVersionsToKeep = 3,
        CancellationToken cancellationToken = default)
    {
        if (_driveService is null)
            throw new InvalidOperationException("Not authenticated");

        var fileInfo = new FileInfo(localFilePath);
        if (!fileInfo.Exists || fileInfo.Length is 0)
            throw new InvalidDataException("El archivo de respaldo local no existe o está vacío.");
        
        // 1. Generar nombre versionado (ej. yomiya_backup_20261116_113000.zip)
        var nameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
        var ext = Path.GetExtension(baseFileName);
        var versionName = $"{nameWithoutExt}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";

        var fileMetadata = new File
        {
            Name = versionName,
            Parents = ["appDataFolder"],
            AppProperties = new Dictionary<string, string>
            {
                ["backup_group"] = baseFileName,
                ["byte_size"] = fileInfo.Length.ToString()
            }
        };
        
        // 2. Subir primero y validar integridad
        await using var stream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var createRequest = _driveService.Files.Create(fileMetadata, stream, BinaryMime);
        createRequest.Fields = "id, size";

        var result = await createRequest.UploadAsync(cancellationToken);

        if (result.Status is not UploadStatus.Completed)
            throw new IOException($"Fallo al subir el respaldo: {result.Exception?.Message}", result.Exception);
        
        if (createRequest.ResponseBody?.Size != fileInfo.Length)
        {
            // Si el tamaño remoto no coincide, borramos el archivo corrupto recién subido y abortamos sin tocar los anteriores
            if (!string.IsNullOrEmpty(createRequest.ResponseBody?.Id))
                await _driveService.Files.Delete(createRequest.ResponseBody.Id).ExecuteAsync(cancellationToken);

            throw new IOException("Validación fallida: el tamaño del archivo en Drive no coincide con el local.");
        }

        // 3. Una vez validado el nuevo respaldo, limpiar versiones viejas conservando las N más recientes
        await PruneOldBackupsAsync(baseFileName, maxVersionsToKeep, cancellationToken);
    }
    
    public async Task<File?> GetLatestBackupAsync(string baseFileName, CancellationToken cancellationToken = default)
    {
        if (_driveService == null)
            throw new InvalidOperationException("Not authenticated");

        var namePrefix = Path.GetFileNameWithoutExtension(baseFileName);
        var request = _driveService.Files.List();
        request.Spaces = "appDataFolder";
        // Busca tanto respaldos versionados nuevos como el formato antiguo exacto
        request.Q = $"(name contains '{namePrefix}' or name = '{baseFileName}') and trashed = false";
        request.Fields = "files(id, name, size, modifiedTime)";

        var result = await request.ExecuteAsync(cancellationToken);
        return result.Files?
            .Where(f => (f.Size ?? 0) > 0)
            .OrderByDescending(x => x.ModifiedTimeDateTimeOffset ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
    }
    
    private async Task PruneOldBackupsAsync(string baseFileName, int maxVersionsToKeep, CancellationToken cancellationToken)
    {
        var namePrefix = Path.GetFileNameWithoutExtension(baseFileName);
        var listReq = _driveService.Files.List();
        listReq.Spaces = "appDataFolder";
        listReq.Q = $"(name contains '{namePrefix}' or name = '{baseFileName}') and trashed = false";
        listReq.Fields = "files(id, modifiedTime)";

        var listRes = await listReq.ExecuteAsync(cancellationToken);
        var backupsToDelete = listRes.Files?
            .OrderByDescending(x => x.ModifiedTimeDateTimeOffset ?? DateTimeOffset.MinValue)
            .Skip(maxVersionsToKeep)
            .ToList();

        if (backupsToDelete == null) return;

        foreach (var oldFile in backupsToDelete)
        {
            try
            {
                await _driveService.Files.Delete(oldFile.Id).ExecuteAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GoogleDriveSync] No se pudo purgar respaldo antiguo {oldFile.Id}: {ex.Message}");
            }
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