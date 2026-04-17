namespace YomiYa.Core.Interfaces;

public interface IConfigurableSource
{
    Task<Dictionary<string, bool>> GetConfigurationAsync();
    Task SetConfigurationAsync(Dictionary<string, bool> configuration);
}