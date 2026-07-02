using ScreenWatch.Core.Models;

namespace ScreenWatch.Core.Data;

public interface IAppRepository
{
    /// <summary>
    /// Finds an app by its exe_path, or creates a new record if not found.
    /// </summary>
    AppInfo? GetOrCreateApp(string exePath, string exeName, string? displayName = null);

    /// <summary>
    /// Gets an app by its primary key.
    /// </summary>
    AppInfo? GetById(int appId);

    /// <summary>
    /// Updates the category for an app and marks it as recognized.
    /// </summary>
    void UpdateCategory(int appId, int categoryId);

    /// <summary>
    /// Updates the display name for an app.
    /// </summary>
    void UpdateDisplayName(int appId, string displayName);

    /// <summary>
    /// Returns all apps that have not yet been recognized (is_recognized = 0).
    /// </summary>
    List<AppInfo> GetUnrecognizedApps();

    /// <summary>
    /// Returns all tracked apps, ordered by display name.
    /// </summary>
    List<AppInfo> GetAllApps();

    /// <summary>
    /// Clears the category assignment for every app (sets category_id to NULL
    /// and is_recognized to 0). Used by the “reset all categories” feature.
    /// </summary>
    void ClearAllCategories();
}
