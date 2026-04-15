using System;
using System.IO;
using System.Threading.Tasks;
using YomiYa.Core.IO;

namespace YomiYa.Core.Services;

public class SyncManager(GoogleDriveSyncService googleDrive)
{
    private readonly string _dbPath = PathHelper.GetDatabasePath();
    private const string BackupFileName = "yomiya_backup.db"; // Nombre del archivo en Google Drive

    // =========================
    // SUBIR RESPALDO
    // =========================
    public async Task<bool> SyncUpAsync()
    {
        if (!await googleDrive.IsAuthenticatedAsync()) return false;

        try
        {
            await googleDrive.UploadOrUpdateBackupAsync(_dbPath, BackupFileName);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SyncManager] Error al subir el respaldo: {ex.Message}");
            return false;
        }
    }

    // =========================
    // DESCARGAR RESPALDO
    // =========================
    public async Task<bool> SyncDownAsync()
    {
        if (!await googleDrive.IsAuthenticatedAsync()) return false;

        try
        {
            var remoteFile = await googleDrive.GetLatestBackupAsync(BackupFileName);
            if (remoteFile != null)
            {
                // 1. Definir ruta temporal
                string tempPath = _dbPath + ".tmp";

                // 2. Descargar el archivo de Drive al archivo temporal
                await googleDrive.DownloadFileAsync(remoteFile.Id, tempPath);

                // 3. Reemplazar la base de datos actual con la descargada
                // IMPORTANTE: Limpiar el pool de SQLite para que libere el archivo y_omiya_library.db
                // De lo contrario, File.Move lanzará un error de "Archivo en uso".
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                
                File.Move(tempPath, _dbPath, overwrite: true);

                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SyncManager] Error al descargar el respaldo: {ex.Message}");
            return false;
        }
    }
}