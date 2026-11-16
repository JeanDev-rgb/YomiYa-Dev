using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Google.Apis.Drive.v3.Data;
using Microsoft.Data.Sqlite;
using YomiYa.Core.Database;
using YomiYa.Core.IPC;
using YomiYa.Core.Services;
using YomiYa.Features.Reader;
using YomiYa.Domain.Models;
using YomiYa.Source.Models;
using YomiYa.Source.Online;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace YomiYa.Tests;

public class CriticalStabilityTests
{
    [Fact]
    public async Task Maintenance_waits_until_active_database_operations_finish()
    {
        var coordinator = new DatabaseOperationCoordinator();
        using var operation = await coordinator.EnterOperationAsync();
        var maintenanceTask = coordinator.EnterMaintenanceAsync();
        await Task.Delay(100);
        Assert.False(maintenanceTask.IsCompleted);

        operation.Dispose();
        using var maintenance = await maintenanceTask.WaitAsync(TimeSpan.FromSeconds(2));
        var nextOperation = coordinator.EnterOperationAsync().AsTask();
        Assert.False(nextOperation.IsCompleted);
        maintenance.Dispose();
        using var resumedOperation = await nextOperation.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Concurrent_plugin_requests_are_correlated_when_responses_arrive_out_of_order()
    {
        var port = GetFreePort();
        using var server = new PluginTcpServer();
        server.Start(port);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        await using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
        await server.WaitForConnectionAsync();

        var firstTask = server.SendRequestAsync("first");
        var secondTask = server.SendRequestAsync("second");
        var firstRequest = JsonSerializer.Deserialize<TcpMessage>(await reader.ReadLineAsync()!)!;
        var secondRequest = JsonSerializer.Deserialize<TcpMessage>(await reader.ReadLineAsync()!)!;
        await writer.WriteLineAsync(JsonSerializer.Serialize(Response(secondRequest.RequestId, "two")));
        await writer.WriteLineAsync(JsonSerializer.Serialize(Response(firstRequest.RequestId, "one")));

        Assert.Equal("one", (await firstTask.WaitAsync(TimeSpan.FromSeconds(2))).GetPayload<string>());
        Assert.Equal("two", (await secondTask.WaitAsync(TimeSpan.FromSeconds(2))).GetPayload<string>());
    }

    [Fact]
    public async Task Old_plugin_disconnect_does_not_drop_reconnected_session()
    {
        var port = GetFreePort();
        using var server = new PluginTcpServer();
        server.Start(port);
        using var oldClient = new TcpClient();
        await oldClient.ConnectAsync(IPAddress.Loopback, port);
        await server.WaitForConnectionAsync();

        using var newClient = new TcpClient();
        await newClient.ConnectAsync(IPAddress.Loopback, port);
        using var reader = new StreamReader(newClient.GetStream());
        await using var writer = new StreamWriter(newClient.GetStream()) { AutoFlush = true };
        await Task.Delay(100);
        await server.WaitForConnectionAsync();
        oldClient.Close();

        var requestTask = server.SendRequestAsync("after-reconnect");
        var request = JsonSerializer.Deserialize<TcpMessage>(await reader.ReadLineAsync()!)!;
        await writer.WriteLineAsync(JsonSerializer.Serialize(Response(request.RequestId, "alive")));
        Assert.Equal("alive", (await requestTask.WaitAsync(TimeSpan.FromSeconds(2))).GetPayload<string>());
    }

    [Fact]
    public async Task Valid_remote_database_replaces_local_database_after_integrity_check()
    {
        var directory = Path.Combine(Path.GetTempPath(), "YomiYa.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "library.db");
        var remotePath = Path.Combine(directory, "remote.db");
        await CreateDatabaseAsync(databasePath, 1);
        await CreateDatabaseAsync(remotePath, 2);
        var drive = new FakeDriveSyncService(remotePath);
        var manager = new SyncManager(drive, new DatabaseOperationCoordinator(), databasePath);

        Assert.True(await manager.SyncDownAsync());
        Assert.Equal(2, await ReadValueAsync(databasePath));
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task Invalid_remote_database_keeps_local_data_unchanged()
    {
        var directory = Path.Combine(Path.GetTempPath(), "YomiYa.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "library.db");
        var remotePath = Path.Combine(directory, "corrupt.db");
        await CreateDatabaseAsync(databasePath, 1);
        await System.IO.File.WriteAllTextAsync(remotePath, "not sqlite");
        var drive = new FakeDriveSyncService(remotePath);
        var manager = new SyncManager(drive, new DatabaseOperationCoordinator(), databasePath);

        Assert.False(await manager.SyncDownAsync());
        Assert.Equal(1, await ReadValueAsync(databasePath));
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task Rapid_chapter_change_keeps_pages_from_the_current_chapter()
    {
        var plugin = new DelayedPagePlugin();
        var mangaService = new MangaService(new EmptyDatabaseService())
        {
            SelectedPlugin = plugin,
            ChapterList = [new SChapter { Name = "Old", Url = "old" }, new SChapter { Name = "New", Url = "new" }],
            ChapterIndex = 0
        };
        using var viewModel = new ReaderViewModel(new EmptyDatabaseService(), mangaService);
        await plugin.OldChapterRequested.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(viewModel.NextChapterCommand.CanExecute(null));
        viewModel.NextChapterCommand.Execute(null);
        Assert.Equal(1, viewModel.ChapterIndex);
        await plugin.NewChapterRequested.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => viewModel.Pages.Count == 1 && viewModel.Pages[0].Url == "new-page");
        plugin.OldPages.TrySetResult([new Page(0, "old-page")]);
        await Task.Delay(100);

        Assert.Equal("new", viewModel.SelectedChapter?.Url);
        Assert.All(viewModel.Pages, page => Assert.Equal("new-page", page.Url));
    }

    private static TcpMessage Response(string requestId, string payload)
    {
        var response = new TcpMessage { Action = "Response", RequestId = requestId };
        response.SetPayload(payload);
        return response;
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeout) throw new TimeoutException("The expected asynchronous state was not reached.");
            await Task.Delay(10);
        }
    }

    private static async Task CreateDatabaseAsync(string path, int value)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE TABLE sample(value INTEGER); INSERT INTO sample VALUES ({value});";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ReadValueAsync(string path)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM sample";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private sealed class FakeDriveSyncService(string remotePath) : IGoogleDriveSyncService
    {
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
        public Task<DriveFile?> GetLatestBackupAsync(string baseFileName, CancellationToken cancellationToken = default) =>
            Task.FromResult<DriveFile?>(new DriveFile { Id = "remote", Name = baseFileName, Size = new FileInfo(remotePath).Length });
        public Task DownloadFileAsync(string fileId, string destinationPath, CancellationToken cancellationToken = default)
        {
            System.IO.File.Copy(remotePath, destinationPath);
            return Task.CompletedTask;
        }
        public Task UploadOrUpdateBackupAsync(string localFilePath, string baseFileName, int maxVersionsToKeep = 3,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class DelayedPagePlugin : ParsedHttpSource
    {
        public TaskCompletionSource OldChapterRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource NewChapterRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<List<Page>> OldPages { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override string BaseUrl => "https://example.test";
        public override string Lang => "en";
        public override string Version => "1";
        public override long Id { get; set; }
        public override string Name { get; set; } = "test";
        public override Task<List<SChapter>> GetChapters(string mangaUrl) => Task.FromResult(new List<SChapter>());
        public override Task<MangasPage> GetLatestUpdates(int page = 1) => throw new NotImplementedException();
        public override Task<SManga> GetMangaDetails(string url) => throw new NotImplementedException();
        public override Task<MangasPage> GetPopularManga(int page = 1) => throw new NotImplementedException();
        public override Task<MangasPage> SearchManga(string query, int page = 1, string genre = "") => throw new NotImplementedException();
        public override Task<List<Page>> GetPages(string chapterUrl)
        {
            if (chapterUrl == "old")
            {
                OldChapterRequested.TrySetResult();
                return OldPages.Task;
            }
            NewChapterRequested.TrySetResult();
            return Task.FromResult(new List<Page> { new(0, "new-page") });
        }
    }

    private sealed class EmptyDatabaseService : IDatabaseService
    {
        public Task ClearHistoryAsync() => Task.CompletedTask;
        public Task DeleteHistoryItemAsync(string chapterUrl) => Task.CompletedTask;
        public Task DeleteMangaAsync(string url) => Task.CompletedTask;
        public Task<List<SChapter>> GetChaptersAsync(string mangaUrl) => Task.FromResult(new List<SChapter>());
        public Task<List<(SManga Manga, SChapter Chapter)>> GetHistoryAsync() => Task.FromResult(new List<(SManga, SChapter)>());
        public Task<List<SManga>> GetLibraryMangasAsync() => Task.FromResult(new List<SManga>());
        public Task<SManga?> GetMangaByUrlAsync(string mangaUrl) => Task.FromResult<SManga?>(null);
        public Task InitializeDatabase() => Task.CompletedTask;
        public Task InsertChapters(long mangaId, System.Collections.Generic.IEnumerable<SChapter> chapters) => Task.CompletedTask;
        public Task<long> InsertManga(SManga manga) => Task.FromResult(1L);
        public Task SetChapterProgress(string chapterUrl, int lastPageRead, bool isRead) => Task.CompletedTask;
        public Task SetMangaFavoriteStatusAsync(SManga manga, bool isFavorite) => Task.CompletedTask;
    }
}
