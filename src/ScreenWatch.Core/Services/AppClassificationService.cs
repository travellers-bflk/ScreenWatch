using ScreenWatch.Core.Data;
using ScreenWatch.Core.Models;

namespace ScreenWatch.Core.Services;

/// <summary>
/// Represents a classification suggestion for an unrecognized app,
/// derived from the built-in rules library.
/// </summary>
public sealed class ClassificationSuggestion
{
    /// <summary>The database id of the suggested category.</summary>
    public int SuggestedCategoryId { get; init; }

    /// <summary>The name of the suggested category.</summary>
    public string SuggestedCategoryName { get; init; } = string.Empty;

    /// <summary>The suggested friendly display name for the app.</summary>
    public string SuggestedDisplayName { get; init; } = string.Empty;
}

/// <summary>
/// Comprehensive app-classification service that combines the app repository,
/// category repository, and built-in rules library to recognize and categorize
/// tracked applications.
/// </summary>
public sealed class AppClassificationService
{
    private readonly IAppRepository _appRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly BuiltinRulesService _builtinRules;

    /// <summary>
    /// Optional event raised when a new unrecognized app is detected.
    /// UI layers may subscribe to this or simply poll <see cref="GetUnrecognizedApps"/>.
    /// </summary>
    public event EventHandler<AppInfo>? NewUnrecognizedAppDetected;

    /// <summary>
    /// Full dependency-injection constructor.
    /// </summary>
    public AppClassificationService(
        IAppRepository appRepo,
        ICategoryRepository categoryRepo,
        BuiltinRulesService builtinRules)
    {
        _appRepo = appRepo;
        _categoryRepo = categoryRepo;
        _builtinRules = builtinRules;
    }

    /// <summary>
    /// Factory method that wires up all production dependencies using DatabaseManager.Instance.
    /// </summary>
    public static AppClassificationService CreateDefault()
    {
        var db = DatabaseManager.Instance;
        db.Initialize();
        return new AppClassificationService(
            new AppRepository(db),
            new CategoryRepository(db),
            new BuiltinRulesService());
    }

    /// <summary>
    /// Returns all apps that have not yet been recognized (is_recognized = 0).
    /// </summary>
    public List<AppInfo> GetUnrecognizedApps() => _appRepo.GetUnrecognizedApps();

    /// <summary>
    /// Assigns a category to an app and marks it as recognized.
    /// </summary>
    public void AssignCategory(int appId, int categoryId)
    {
        _appRepo.UpdateCategory(appId, categoryId);
    }

    /// <summary>
    /// Assigns a category to an app by category name.
    /// Throws <see cref="InvalidOperationException"/> if the category does not exist.
    /// </summary>
    public void AssignCategoryByName(int appId, string categoryName)
    {
        var category = FindCategoryByName(categoryName)
            ?? throw new InvalidOperationException($"分类 '{categoryName}' 不存在。");

        _appRepo.UpdateCategory(appId, category.CategoryId);
    }

    /// <summary>
    /// Generates a classification suggestion for the given app using the built-in rules.
    /// Returns null if no rule matches or the suggested category does not exist in the database.
    /// </summary>
    public ClassificationSuggestion? SuggestForApp(AppInfo app)
    {
        var rule = _builtinRules.FindRule(app.ExeName);
        if (rule == null)
            return null;

        var category = FindCategoryByName(rule.Category);
        if (category == null)
            return null;

        return new ClassificationSuggestion
        {
            SuggestedCategoryId = category.CategoryId,
            SuggestedCategoryName = category.Name,
            SuggestedDisplayName = rule.DisplayName
        };
    }

    /// <summary>
    /// Batch-classifies all unrecognized apps that have a matching built-in rule.
    /// Returns the number of apps that were automatically classified.
    /// </summary>
    public int AutoClassifyAllUnrecognized()
    {
        var apps = _appRepo.GetUnrecognizedApps();
        if (apps.Count == 0)
            return 0;

        // Load categories once for efficient lookup
        var categories = _categoryRepo.GetAll();
        var categoryLookup = categories
            .ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        int classified = 0;
        foreach (var app in apps)
        {
            var rule = _builtinRules.FindRule(app.ExeName);
            if (rule == null)
                continue;

            if (!categoryLookup.TryGetValue(rule.Category, out var category))
                continue;

            _appRepo.UpdateCategory(app.AppId, category.CategoryId);
            classified++;
        }

        return classified;
    }

    // ================================================================
    //  Keyword-based auto-categorisation (CategoryDefinitions)
    // ================================================================

    /// <summary>
    /// Auto-categorises all currently unrecognised apps using the keyword-based
    /// <see cref="CategoryDefinitions.AutoCategorize"/> algorithm. Apps that are
    /// already recognised (have a category assigned) are left untouched.
    /// Returns the number of apps that were classified.
    /// </summary>
    public int AutoCategorizeAllApps()
    {
        var apps = _appRepo.GetUnrecognizedApps();
        if (apps.Count == 0)
            return 0;

        // Load categories once for efficient lookup by name
        var categoryLookup = _categoryRepo.GetAll()
            .ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        int classified = 0;
        foreach (var app in apps)
        {
            var categoryName = CategoryDefinitions.AutoCategorize(
                app.DisplayName, app.ExeName, app.ExePath);

            if (categoryLookup.TryGetValue(categoryName, out var category))
            {
                _appRepo.UpdateCategory(app.AppId, category.CategoryId);
                classified++;
            }
        }

        return classified;
    }

    /// <summary>
    /// Resets every app's category assignment (clears category_id and marks
    /// all apps as unrecognised), then re-runs keyword-based auto-categorisation
    /// on the full app list. Returns the number of apps that were classified.
    /// </summary>
    public int ResetAllCategories()
    {
        _appRepo.ClearAllCategories();
        return AutoCategorizeAllApps();
    }

    /// <summary>
    /// Raises the <see cref="NewUnrecognizedAppDetected"/> event. Can be called by
    /// upstream components (e.g. the tracking service) to notify the UI layer.
    /// </summary>
    internal void RaiseNewUnrecognizedAppDetected(AppInfo app)
    {
        NewUnrecognizedAppDetected?.Invoke(this, app);
    }

    /// <summary>
    /// Finds a category by name using case-insensitive comparison.
    /// </summary>
    private Category? FindCategoryByName(string name)
    {
        return _categoryRepo.GetAll()
            .FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
