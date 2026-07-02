using System.IO;
using System.Security.Cryptography;
using System.Text;
using ScreenWatch.Core.Data;
using ScreenWatch.Core.Models;
using ScreenWatch.Core.Native;

namespace ScreenWatch.Core.Services;

/// <summary>
/// Normalizes raw <see cref="WindowInfo"/> into persisted <see cref="AppInfo"/> records
/// and provides an in-memory icon cache backed by optional disk persistence.
/// </summary>
public class AppResolver
{
    private readonly IAppRepository _appRepository;
    private readonly IWindowService _windowService;
    private readonly Dictionary<string, byte[]> _iconCache = new();
    private readonly object _iconLock = new();
    private readonly string _iconDir;

    /// <summary>
    /// Creates an AppResolver that uses the given repository and window service.
    /// </summary>
    /// <param name="appRepository">Repository for app persistence.</param>
    /// <param name="windowService">Window service used for icon extraction.</param>
    /// <param name="iconDir">Optional directory for persistent icon cache. Defaults to %APPDATA%\ScreenWatch\data\icons.</param>
    public AppResolver(IAppRepository appRepository, IWindowService windowService, string? iconDir = null)
    {
        _appRepository = appRepository;
        _windowService = windowService;
        _iconDir = iconDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScreenWatch", "data", "icons");

        LoadPersistedIcons();
    }

    /// <summary>
    /// Resolves a <see cref="WindowInfo"/> to a persisted <see cref="AppInfo"/>.
    /// If ExePath is empty (high-privilege process), ProcessName is used as a fallback key.
    /// </summary>
    /// <param name="window">The window to resolve.</param>
    /// <returns>The resolved AppInfo, or null if resolution fails.</returns>
    public AppInfo? ResolveApp(WindowInfo window)
    {
        // Determine the identifying key: prefer ExePath, fall back to ProcessName
        var exePath = !string.IsNullOrEmpty(window.ExePath)
            ? window.ExePath
            : window.ProcessName;

        if (string.IsNullOrEmpty(exePath))
            return null;

        // ExeName: prefer ProcessName (includes .exe), fall back to file name from path
        var exeName = !string.IsNullOrEmpty(window.ProcessName)
            ? window.ProcessName
            : Path.GetFileName(exePath);

        if (string.IsNullOrEmpty(exeName))
            exeName = "unknown.exe";

        // DisplayName: ProcessName without .exe suffix
        var displayName = GetDisplayName(window.ProcessName, exeName);

        try
        {
            return _appRepository.GetOrCreateApp(exePath, exeName, displayName);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Retrieves the icon bytes for the given exe path, using an in-memory cache
    /// backed by optional disk persistence.
    /// </summary>
    /// <param name="exePath">The exe path to extract the icon from.</param>
    /// <returns>PNG bytes of the icon, or null if extraction fails.</returns>
    public byte[]? GetIcon(string exePath)
    {
        if (string.IsNullOrEmpty(exePath))
            return null;

        lock (_iconLock)
        {
            if (_iconCache.TryGetValue(exePath, out var cached))
                return cached;
        }

        // Disk cache lookup
        var diskKey = GetIconCacheKey(exePath);
        var diskPath = Path.Combine(_iconDir, diskKey + ".png");
        if (File.Exists(diskPath))
        {
            try
            {
                var bytes = File.ReadAllBytes(diskPath);
                lock (_iconLock)
                {
                    _iconCache[exePath] = bytes;
                }
                return bytes;
            }
            catch
            {
                // Disk read failed; fall through to extraction
            }
        }

        // Extract from exe
        byte[]? iconBytes = null;
        try
        {
            iconBytes = _windowService.ExtractIconBytes(exePath);
        }
        catch
        {
            iconBytes = null;
        }

        if (iconBytes != null)
        {
            lock (_iconLock)
            {
                _iconCache[exePath] = iconBytes;
            }

            // Persist to disk (best-effort)
            try
            {
                Directory.CreateDirectory(_iconDir);
                File.WriteAllBytes(diskPath, iconBytes);
            }
            catch
            {
                // Disk write failed; cache is still in memory
            }
        }

        return iconBytes;
    }

    /// <summary>
    /// Generates a stable cache key from an exe path using SHA-256 hashing.
    /// </summary>
    private static string GetIconCacheKey(string exePath)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(exePath.ToLowerInvariant()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Derives a display name from the process name by stripping the .exe suffix.
    /// </summary>
    private static string GetDisplayName(string processName, string exeName)
    {
        var source = !string.IsNullOrEmpty(processName) ? processName : exeName;
        if (source.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return source[..^4];
        return source;
    }

    /// <summary>
    /// Loads any previously persisted icons from disk into the in-memory cache.
    /// </summary>
    private void LoadPersistedIcons()
    {
        // Icons are loaded lazily on GetIcon calls; this method is a placeholder
        // for future bulk-loading optimization. The disk cache is checked per-key
        // in GetIcon, so we don't need to enumerate all files here.
    }
}
