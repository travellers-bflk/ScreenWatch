namespace ScreenWatch.Core.Data;

/// <summary>
/// Represents a single database migration with a version number, name, and SQL script.
/// </summary>
public record Migration(int Version, string ScriptName, string Sql);
