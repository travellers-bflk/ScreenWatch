namespace ScreenWatch.Core.Data;

/// <summary>
/// Migration v2: adds the <c>icon</c> column to the categories table,
/// seeds four additional built-in categories (开发, 工具, 媒体, 游戏),
/// and back-fills emoji icons onto the five original categories.
/// </summary>
public static class MigrationV2
{
    public const string Sql = """
        -- Add icon column (nullable, defaults to NULL for user-created categories)
        ALTER TABLE categories ADD COLUMN icon TEXT;

        -- Seed four additional built-in categories
        INSERT OR IGNORE INTO categories (name, color, icon) VALUES ('开发', '#9B59B6', '💻');
        INSERT OR IGNORE INTO categories (name, color, icon) VALUES ('工具', '#3498DB', '🔧');
        INSERT OR IGNORE INTO categories (name, color, icon) VALUES ('媒体', '#E67E22', '🎬');
        INSERT OR IGNORE INTO categories (name, color, icon) VALUES ('游戏', '#E91E63', '🕹️');

        -- Back-fill emoji icons onto the five original categories
        UPDATE categories SET icon = '💼' WHERE name = '工作' AND icon IS NULL;
        UPDATE categories SET icon = '🎮' WHERE name = '娱乐' AND icon IS NULL;
        UPDATE categories SET icon = '📚' WHERE name = '学习' AND icon IS NULL;
        UPDATE categories SET icon = '💬' WHERE name = '社交' AND icon IS NULL;
        UPDATE categories SET icon = '📦' WHERE name = '其他' AND icon IS NULL;
        """;
}
