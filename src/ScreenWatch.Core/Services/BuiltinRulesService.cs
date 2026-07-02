using System.Reflection;
using System.Text.Json;

namespace ScreenWatch.Core.Services;

/// <summary>
/// Represents a single built-in classification rule mapping an exe name
/// to a default category and a friendly display name.
/// </summary>
public sealed record BuiltinRule(string ExeName, string Category, string DisplayName);

/// <summary>
/// Loads the embedded <c>builtin_rules.json</c> resource and provides
/// case-insensitive lookup of common Windows applications by exe name.
/// </summary>
public sealed class BuiltinRulesService
{
    private readonly Dictionary<string, BuiltinRule> _rules;

    /// <summary>
    /// Creates a new instance and loads rules from the embedded JSON resource.
    /// </summary>
    public BuiltinRulesService()
    {
        _rules = LoadEmbeddedRules();
    }

    /// <summary>
    /// Creates a new instance from an explicit JSON string. Primarily for testing.
    /// </summary>
    /// <param name="json">A JSON document conforming to the builtin_rules.json format.</param>
    public BuiltinRulesService(string json)
    {
        _rules = ParseRules(json);
    }

    /// <summary>
    /// Finds the built-in rule for the given exe name using case-insensitive comparison.
    /// Returns null when no rule matches.
    /// </summary>
    public BuiltinRule? FindRule(string exeName)
    {
        if (string.IsNullOrEmpty(exeName))
            return null;

        return _rules.TryGetValue(exeName, out var rule) ? rule : null;
    }

    /// <summary>
    /// Returns the suggested category name for the given exe name, or null if no match.
    /// </summary>
    public string? SuggestCategory(string exeName)
    {
        return FindRule(exeName)?.Category;
    }

    /// <summary>
    /// Returns the suggested friendly display name for the given exe name, or null if no match.
    /// </summary>
    public string? SuggestDisplayName(string exeName)
    {
        return FindRule(exeName)?.DisplayName;
    }

    /// <summary>
    /// Returns all loaded rules (read-only view).
    /// </summary>
    public IReadOnlyCollection<BuiltinRule> GetAllRules() => _rules.Values;

    // ----------------------------------------------------------------
    //  Loading helpers
    // ----------------------------------------------------------------

    private static Dictionary<string, BuiltinRule> LoadEmbeddedRules()
    {
        var assembly = typeof(BuiltinRulesService).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("builtin_rules.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
            throw new InvalidOperationException(
                "Embedded resource 'builtin_rules.json' was not found. " +
                "Ensure the file is configured as an EmbeddedResource in the project.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Manifest resource stream '{resourceName}' could not be opened.");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return ParseRules(json);
    }

    private static Dictionary<string, BuiltinRule> ParseRules(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<BuiltinRulesFile>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize builtin_rules.json.");

        var dict = new Dictionary<string, BuiltinRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in file.Rules)
        {
            if (string.IsNullOrWhiteSpace(entry.ExeName))
                continue;
            // Later entries with the same exe name overwrite earlier ones
            dict[entry.ExeName] = new BuiltinRule(entry.ExeName, entry.Category, entry.DisplayName);
        }
        return dict;
    }

    // ----------------------------------------------------------------
    //  JSON DTOs (private)
    // ----------------------------------------------------------------

    private sealed class BuiltinRulesFile
    {
        public List<BuiltinRuleEntry> Rules { get; set; } = new();
    }

    private sealed class BuiltinRuleEntry
    {
        public string ExeName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
