using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Drive.v3.Data;

namespace YomiYa.Core.Services;

public interface IGoogleDriveSyncService
{
    Task<bool> IsAuthenticatedAsync();
    Task<File?> GetLatestBackupAsync(string baseFileName, CancellationToken cancellationToken = default);
    Task DownloadFileAsync(string fileId, string destinationPath, CancellationToken cancellationToken = default);
    Task UploadOrUpdateBackupAsync(string localFilePath, string baseFileName, int maxVersionsToKeep = 3,
        CancellationToken cancellationToken = default);
}
