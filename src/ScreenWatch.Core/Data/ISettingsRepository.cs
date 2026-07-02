namespace ScreenWatch.Core.Data;

public interface ISettingsRepository
{
    string? Get(string key);
    void Set(string key, string value);

    /// <summary>
    /// Gets a setting value and deserializes it to the specified type,
    /// returning the default value if the key does not exist or parsing fails.
    /// </summary>
    T Get<T>(string key, T defaultValue);
}
