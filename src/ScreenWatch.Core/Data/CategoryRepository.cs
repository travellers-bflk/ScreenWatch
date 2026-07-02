using Microsoft.Data.Sqlite;
using ScreenWatch.Core.Models;

namespace ScreenWatch.Core.Data;

public class CategoryRepository : ICategoryRepository
{
    private readonly DatabaseManager _db;

    public CategoryRepository(DatabaseManager db)
    {
        _db = db;
    }

    public List<Category> GetAll()
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM categories ORDER BY name";
        using var reader = cmd.ExecuteReader();
        var categories = new List<Category>();
        while (reader.Read())
        {
            categories.Add(MapCategory(reader));
        }
        return categories;
    }

    public Category? GetById(int id)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM categories WHERE category_id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapCategory(reader) : null;
    }

    public int Add(string name, string color, string? icon = null)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO categories (name, color, icon) VALUES (@name, @color, @icon);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@color", (object?)color ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@icon", (object?)icon ?? DBNull.Value);
            return (int)(long)cmd.ExecuteScalar()!;
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    public void Update(int id, string name, string color, string? icon = null)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE categories SET name = @name, color = @color, icon = @icon WHERE category_id = @id";
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@color", (object?)color ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@icon", (object?)icon ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    public void Delete(int id)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE apps SET category_id = NULL, is_recognized = 0 WHERE category_id = @id;
                DELETE FROM categories WHERE category_id = @id;
                """;
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    private static Category MapCategory(SqliteDataReader reader)
    {
        return new Category
        {
            CategoryId = reader.GetInt("category_id"),
            Name = reader.GetString("name"),
            Color = reader.GetNullableString("color") ?? string.Empty,
            Icon = reader.GetNullableString("icon")
        };
    }
}
