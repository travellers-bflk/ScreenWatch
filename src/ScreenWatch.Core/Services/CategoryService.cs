using ScreenWatch.Core.Data;
using ScreenWatch.Core.Models;

namespace ScreenWatch.Core.Services;

/// <summary>
/// Business-layer service for managing application categories.
/// Wraps <see cref="ICategoryRepository"/> and enforces referential-integrity
/// rules when deleting categories that are still referenced by apps.
/// </summary>
public sealed class CategoryService
{
    private readonly ICategoryRepository _categoryRepo;
    private readonly IAppRepository _appRepo;
    private readonly DatabaseManager _db;

    /// <summary>
    /// Full dependency-injection constructor.
    /// </summary>
    /// <param name="categoryRepo">The category repository.</param>
    /// <param name="appRepo">The app repository (used for referential-integrity checks).</param>
    /// <param name="db">The database manager (used for bulk unassign operations).</param>
    public CategoryService(ICategoryRepository categoryRepo, IAppRepository appRepo, DatabaseManager db)
    {
        _categoryRepo = categoryRepo;
        _appRepo = appRepo;
        _db = db;
    }

    /// <summary>
    /// Factory method that wires up all production dependencies using DatabaseManager.Instance.
    /// </summary>
    public static CategoryService CreateDefault()
    {
        var db = DatabaseManager.Instance;
        db.Initialize();
        return new CategoryService(new CategoryRepository(db), new AppRepository(db), db);
    }

    /// <summary>
    /// Returns all categories, ordered by name.
    /// </summary>
    public List<Category> GetAllCategories() => _categoryRepo.GetAll();

    /// <summary>
    /// Returns the category with the given id, or null if not found.
    /// </summary>
    public Category? GetCategory(int id) => _categoryRepo.GetById(id);

    /// <summary>
    /// Creates a new category and returns its generated id.
    /// </summary>
    public int AddCategory(string name, string color, string? icon = null) => _categoryRepo.Add(name, color, icon);

    /// <summary>
    /// Updates the name and color of an existing category.
    /// </summary>
    public void UpdateCategory(int id, string name, string color, string? icon = null) => _categoryRepo.Update(id, name, color, icon);

    /// <summary>
    /// Deletes a category. Throws <see cref="InvalidOperationException"/> if one or more
    /// apps are still assigned to the category — use <see cref="DeleteCategoryAndUnassign"/>
    /// to force-remove the category while clearing app references.
    /// </summary>
    public void DeleteCategory(int id)
    {
        var referencingApps = _appRepo.GetAllApps().Where(a => a.CategoryId == id).ToList();
        if (referencingApps.Count > 0)
        {
            throw new InvalidOperationException(
                $"无法删除分类：仍有 {referencingApps.Count} 个应用引用此分类。" +
                "请先为这些应用重新指定分类，或使用 DeleteCategoryAndUnassign 解除关联后再删除。");
        }

        _categoryRepo.Delete(id);
    }

    /// <summary>
    /// Deletes a category and unassigns all apps that referenced it
    /// (sets their category_id to NULL and is_recognized to 0).
    /// </summary>
    public void DeleteCategoryAndUnassign(int id)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE apps SET category_id = NULL, is_recognized = 0
                WHERE category_id = @id
                """;
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
        finally
        {
            _db.WriteLock.Release();
        }

        _categoryRepo.Delete(id);
    }
}
