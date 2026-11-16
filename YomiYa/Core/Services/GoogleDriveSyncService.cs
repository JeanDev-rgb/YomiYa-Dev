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

public class GoogleDriveSyncService : IGoogleDriveSyncService
{
    private const string ApplicationName = "YomiYa";
    private const string BinaryMime = "application/octet-stream";
    private static readonly string[] Scopes = { DriveService.Scope.DriveAppdata };

    private DriveService? _driveService;
    private readonly Task _silentLoginTask;

    public GoogleDriveSyncService()
    {
        _silentLoginTask = TrySilentLoginAsync();
    }

    public bool IsAuthenticated => _driveService is not null;

    private async Task TrySilentLoginAsync()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            await using var stream = assembly.GetManifestResourceStream("YomiYa.credentials.json");
            if (stream is null) return;

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
            await _silentLoginTask.WaitAsync(cancellationToken);
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
            await _silentLoginTask;
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

    public async Task DownloadFileAsync(string fileId, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (_driveService is null)
            throw new InvalidOperationException("Not authenticated");

        try
        {
            var request = _driveService.Files.Get(fileId);
            request.Fields = "id,size,name";
            var metadata = await request.ExecuteAsync(cancellationToken);
            if (metadata.Size is null or <= 0)
                throw new InvalidDataException("El archivo remoto no tiene un tamaño válido.");

            await using var fileStream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

            await request.DownloadAsync(fileStream, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);
            if (fileStream.Length <= 0 || fileStream.Length != metadata.Size.Value)
                throw new InvalidDataException("El tamaño descargado no coincide con el tamaño declarado por Drive.");
        }
        catch (Exception e)
        {
            try { if (System.IO.File.Exists(destinationPath)) System.IO.File.Delete(destinationPath); }
            catch (Exception cleanupError) { Console.WriteLine($"[GoogleDriveSync] No se pudo limpiar descarga parcial: {cleanupError.Message}"); }
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
        if (maxVersionsToKeep < 1) throw new ArgumentOutOfRangeException(nameof(maxVersionsToKeep));

        var fileInfo = new FileInfo(localFilePath);
        if (!fileInfo.Exists || fileInfo.Length is 0)
            throw new InvalidDataException("El archivo de respaldo local no existe o está vacío.");
        
        // 1. Generar nombre versionado (ej. yomiya_backup_20261116_113000.zip)
        var nameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
        var ext = Path.GetExtension(baseFileName);
        var versionName = $"{nameWithoutExt}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}{ext}";

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

        var result = await ListBackupsAsync(baseFileName, cancellationToken);
        return result
            .Where(f => (f.Size ?? 0) > 0 && IsBackupCandidate(f, baseFileName))
            .OrderByDescending(x => x.ModifiedTimeDateTimeOffset ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
    }
    
    private async Task PruneOldBackupsAsync(string baseFileName, int maxVersionsToKeep, CancellationToken cancellationToken)
    {
        var driveService = _driveService ?? throw new InvalidOperationException("Not authenticated");
        var backupsToDelete = (await ListBackupsAsync(baseFileName, cancellationToken))
            .Where(f => IsManagedBackup(f, baseFileName))
            .OrderByDescending(x => x.ModifiedTimeDateTimeOffset ?? DateTimeOffset.MinValue)
            .Skip(maxVersionsToKeep)
            .ToList();

        foreach (var oldFile in backupsToDelete)
        {
            try
            {
                await driveService.Files.Delete(oldFile.Id).ExecuteAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GoogleDriveSync] No se pudo purgar respaldo antiguo {oldFile.Id}: {ex.Message}");
            }
        }
    }

    private async Task<List<File>> ListBackupsAsync(string baseFileName, CancellationToken cancellationToken)
    {
        if (_driveService is null) throw new InvalidOperationException("Not authenticated");
        var namePrefix = Path.GetFileNameWithoutExtension(baseFileName).Replace("'", "\\'");
        var exactName = baseFileName.Replace("'", "\\'");
        var request = _driveService.Files.List();
        request.Spaces = "appDataFolder";
        request.Q = $"(name contains '{namePrefix}' or name = '{exactName}') and trashed = false";
        request.PageSize = 1000;
        request.Fields = "nextPageToken,files(id,name,size,modifiedTime,appProperties)";
        var files = new List<File>();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await request.ExecuteAsync(cancellationToken);
            if (page.Files is not null) files.AddRange(page.Files);
            request.PageToken = page.NextPageToken;
        } while (!string.IsNullOrEmpty(request.PageToken));
        return files;
    }

    private static bool IsManagedBackup(File file, string baseFileName) =>
        file.AppProperties?.TryGetValue("backup_group", out var group) == true
            ? string.Equals(group, baseFileName, StringComparison.Ordinal)
            : string.Equals(file.Name, baseFileName, StringComparison.Ordinal);

    private static bool IsBackupCandidate(File file, string baseFileName)
    {
        if (!IsManagedBackup(file, baseFileName)) return false;
        if (string.Equals(file.Name, baseFileName, StringComparison.Ordinal)) return true;
        var stem = Path.GetFileNameWithoutExtension(baseFileName);
        var ext = Path.GetExtension(baseFileName);
        return file.Name?.StartsWith(stem + "_", StringComparison.Ordinal) == true &&
               file.Name.EndsWith(ext, StringComparison.Ordinal);
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
