namespace YomiYa.Core.Settings
{
    public interface ISettingsService
    {
        AppSettings Settings { get; }

        void Load();
        void Save();
    }

}