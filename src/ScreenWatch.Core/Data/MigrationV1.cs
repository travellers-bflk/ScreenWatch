namespace ScreenWatch.Core.Data;

/// <summary>
/// Contains the SQL script for the initial v1 database schema.
/// Creates all base tables, indexes, and default category data.
/// </summary>
public static class MigrationV1
{
    public const string Sql = """
        -- Categories table (created first for FK references)
        CREATE TABLE IF NOT EXISTS categories (
            category_id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL UNIQUE,
            color TEXT
        );

        -- Apps table
        CREATE TABLE IF NOT EXISTS apps (
            app_id INTEGER PRIMARY KEY AUTOINCREMENT,
            exe_path TEXT UNIQUE NOT NULL,
            exe_name TEXT NOT NULL,
            display_name TEXT,
            category_id INTEGER,
            icon_cache_key TEXT,
            first_seen TEXT NOT NULL,
            is_recognized INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY (category_id) REFERENCES categories(category_id)
        );

        -- Usage sessions table
        CREATE TABLE IF NOT EXISTS usage_sessions (
            session_id INTEGER PRIMARY KEY AUTOINCREMENT,
            app_id INTEGER NOT NULL,
            session_type TEXT NOT NULL,
            start_time TEXT NOT NULL,
            end_time TEXT NOT NULL,
            duration_sec INTEGER NOT NULL,
            is_idle INTEGER NOT NULL DEFAULT 0,
            is_locked INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY (app_id) REFERENCES apps(app_id)
        );

        CREATE INDEX IF NOT EXISTS idx_sessions_start ON usage_sessions(start_time);
        CREATE INDEX IF NOT EXISTS idx_sessions_app ON usage_sessions(app_id);
        CREATE INDEX IF NOT EXISTS idx_sessions_type ON usage_sessions(session_type);

        -- Exclusion whitelist table
        CREATE TABLE IF NOT EXISTS exclusion_whitelist (
            rule_id INTEGER PRIMARY KEY AUTOINCREMENT,
            match_type TEXT NOT NULL,
            pattern TEXT NOT NULL,
            enabled INTEGER NOT NULL DEFAULT 1
        );

        -- Time periods table
        CREATE TABLE IF NOT EXISTS time_periods (
            period_id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            start_time TEXT NOT NULL,
            end_time TEXT NOT NULL,
            weekdays TEXT,
            enabled INTEGER NOT NULL DEFAULT 1
        );

        -- Settings table
        CREATE TABLE IF NOT EXISTS settings (
            key TEXT PRIMARY KEY,
            value TEXT
        );

        -- Default categories
        INSERT OR IGNORE INTO categories (name, color) VALUES ('工作', '#4A90D9');
        INSERT OR IGNORE INTO categories (name, color) VALUES ('娱乐', '#E74C3C');
        INSERT OR IGNORE INTO categories (name, color) VALUES ('学习', '#2ECC71');
        INSERT OR IGNORE INTO categories (name, color) VALUES ('社交', '#F39C12');
        INSERT OR IGNORE INTO categories (name, color) VALUES ('其他', '#95A5A6');
        """;
}
