using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using YomiYa.Core.Database;
using YomiYa.Core.IO;

namespace YomiYa.Core.Services;

public class SyncManager(IGoogleDriveSyncService googleDrive, DatabaseOperationCoordinator databaseOperations, string? databasePath = null)
{
    private const string BackupFileName = "yomiya_backup.db";
    private readonly string _dbPath = databasePath ?? PathHelper.GetDatabasePath();

    public async Task<bool> SyncUpAsync(CancellationToken cancellationToken = default)
    {
        if (!await googleDrive.IsAuthenticatedAsync()) return false;
        var snapshot = UniquePath("upload");
        try
        {
            using (await databaseOperations.EnterMaintenanceAsync(cancellationToken))
            {
                if (!System.IO.File.Exists(_dbPath)) return false;
                await CheckpointAsync(_dbPath, cancellationToken);
                SqliteConnection.ClearAllPools();
                System.IO.File.Copy(_dbPath, snapshot);
            }
            await googleDrive.UploadOrUpdateBackupAsync(snapshot, BackupFileName, cancellationToken: cancellationToken);
            Console.WriteLine("[SyncManager] Respaldo enviado correctamente.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SyncManager] Error al subir el respaldo: {ex}");
            return false;
        }
        finally { TryDelete(snapshot); }
    }

    public async Task<bool> SyncDownAsync(CancellationToken cancellationToken = default)
    {
        if (!await googleDrive.IsAuthenticatedAsync()) return false;
        var tempPath = UniquePath("restore");
        var localBackup = UniquePath("before-restore");
        var replaced = false;
        try
        {
            Console.WriteLine("[SyncManager] Buscando el respaldo remoto más reciente.");
            var remoteFile = await googleDrive.GetLatestBackupAsync(BackupFileName, cancellationToken);
            if (remoteFile is null) return false;

            await googleDrive.DownloadFileAsync(remoteFile.Id, tempPath, cancellationToken);
            var downloadedLength = new FileInfo(tempPath).Length;
            if (downloadedLength <= 0 || (remoteFile.Size.HasValue && downloadedLength != remoteFile.Size.Value))
                throw new InvalidDataException("El tamaño del respaldo descargado no coincide con el tamaño remoto.");
            await ValidateDatabaseAsync(tempPath, cancellationToken);

            using (await databaseOperations.EnterMaintenanceAsync(cancellationToken))
            {
                Console.WriteLine("[SyncManager] Esperando el cierre de operaciones SQLite activas.");
                await CheckpointAsync(_dbPath, cancellationToken);
                SqliteConnection.ClearAllPools();

                if (System.IO.File.Exists(_dbPath)) System.IO.File.Copy(_dbPath, localBackup);
                try
                {
                    DeleteSidecar(_dbPath + "-wal");
                    DeleteSidecar(_dbPath + "-shm");
                    System.IO.File.Move(tempPath, _dbPath, true);
                    replaced = true;
                    await ValidateDatabaseAsync(_dbPath, cancellationToken);
                }
                catch
                {
                    if (System.IO.File.Exists(localBackup)) System.IO.File.Move(localBackup, _dbPath, true);
                    DeleteSidecar(_dbPath + "-wal");
                    DeleteSidecar(_dbPath + "-shm");
                    throw;
                }
            }

            Console.WriteLine("[SyncManager] Restauración completada y validada.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SyncManager] Error al restaurar; se conserva la base local: {ex}");
            if (replaced && System.IO.File.Exists(localBackup))
            {
                try { System.IO.File.Move(localBackup, _dbPath, true); }
                catch (Exception rollbackError) { Console.WriteLine($"[SyncManager] Falló la recuperación local: {rollbackError}"); }
            }
            return false;
        }
        finally
        {
            TryDelete(tempPath);
            TryDelete(localBackup);
        }
    }

    private static async Task ValidateDatabaseAsync(string path, CancellationToken cancellationToken)
    {
        if (!System.IO.File.Exists(path) || new FileInfo(path).Length <= 0)
            throw new InvalidDataException("El archivo de base de datos está vacío.");
        var builder = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"SQLite integrity_check falló: {result ?? "sin resultado"}.");
    }

    private static async Task CheckpointAsync(string path, CancellationToken cancellationToken)
    {
        if (!System.IO.File.Exists(path)) return;
        var builder = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string UniquePath(string purpose) => _dbPath + "." + purpose + "." + Guid.NewGuid().ToString("N");

    private static void TryDelete(string path)
    {
        try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
        catch (Exception ex) { Console.WriteLine($"[SyncManager] No se pudo limpiar {path}: {ex.Message}"); }
    }

    private static void DeleteSidecar(string path)
    {
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }
}
