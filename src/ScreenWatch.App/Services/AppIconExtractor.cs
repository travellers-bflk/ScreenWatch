using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ScreenWatch.App.Services;

/// <summary>
/// 应用图标提取服务：从本地 .exe 文件提取图标并转换为 WPF 可绑定的 <see cref="BitmapImage"/>。
/// 采用 SHA256 哈希命名缓存文件以保护用户隐私，所有操作均在本地完成，无任何网络请求。
/// </summary>
public class AppIconExtractor : IDisposable
{
    // ===== 单例 =====

    private static readonly Lazy<AppIconExtractor> _instance = new(() => new AppIconExtractor());

    /// <summary>单例实例（线程安全延迟初始化）</summary>
    public static AppIconExtractor Instance => _instance.Value;

    // ===== P/Invoke 声明 =====

    /// <summary>
    /// 从可执行文件提取关联图标。该函数会修改 lpszPath（追加图标索引），
    /// 因此使用 StringBuilder 而非 string 以预留缓冲区空间。
    /// </summary>
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ExtractAssociatedIcon(
        IntPtr hInst, StringBuilder lpszPath, ref int lpiIcon);

    /// <summary>销毁图标句柄，防止 GDI 泄漏</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>获取文件信息（用于提取高质量大图标）</summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x00000100;
    private const uint SHGFI_LARGEICON = 0x00000000;

    // ===== 缓存配置 =====

    /// <summary>图标缓存目录：%LOCALAPPDATA%\ScreenWatch\icon_cache</summary>
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenWatch", "icon_cache");

    /// <summary>缓存过期时间：7 天</summary>
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromDays(7);

    // ===== 内存缓存（WeakReference 避免内存泄漏）=====

    private readonly Dictionary<string, WeakReference<BitmapImage>> _memoryCache = new();
    private readonly object _cacheLock = new();
    private bool _disposed;

    // ===== 统计计数（不含路径信息，仅用于性能监控）=====

    private int _totalRequests;
    private int _cacheHits;
    private int _extractionSuccess;
    private int _extractionFail;

    /// <summary>
    /// 创建 AppIconExtractor 实例并确保缓存目录存在。
    /// </summary>
    private AppIconExtractor()
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
        }
        catch
        {
            // 缓存目录创建失败不影响提取功能，仅无法持久化缓存
        }
    }

    // ===== 公共 API =====

    /// <summary>
    /// 异步提取指定 exe 文件的图标。
    /// 读取顺序：内存缓存 → 文件缓存 → Win32 API 提取并缓存。
    /// </summary>
    /// <param name="exePath">exe 文件完整路径（仅支持本地磁盘路径，拒绝网络路径）</param>
    /// <param name="size">目标图标尺寸（像素），支持 16/32/48/256，默认 32</param>
    /// <returns>WPF 可绑定的 BitmapImage；提取失败返回默认应用图标</returns>
    /// <exception cref="ArgumentException">路径为网络路径（UNC）或非法路径</exception>
    /// <exception cref="ObjectDisposedException">对象已释放</exception>
    public async Task<BitmapImage> ExtractIconAsync(string exePath, int size = 32)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AppIconExtractor));

        if (!IsLocalPath(exePath))
            throw new ArgumentException("仅支持本地应用图标提取，拒绝网络路径", nameof(exePath));

        if (!File.Exists(exePath))
            return GetDefaultIcon(size);

        Interlocked.Increment(ref _totalRequests);

        var cacheKey = GetCacheKey(exePath, size);

        // 1. 内存缓存
        if (TryGetMemoryCache(cacheKey, out var cached) && cached != null)
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        // 2. 文件缓存
        var cacheFile = GetCacheFilePath(cacheKey);
        if (File.Exists(cacheFile))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(cacheFile);
                var image = CreateBitmapImage(bytes, size);
                if (image != null)
                {
                    SetMemoryCache(cacheKey, image);
                    Interlocked.Increment(ref _cacheHits);
                    return image;
                }
            }
            catch
            {
                // 文件缓存损坏，继续提取
            }
        }

        // 3. 调用 Win32 API 提取（后台线程，避免阻塞 UI）
        return await Task.Run(() =>
        {
            var bytes = ExtractIconBytes(exePath, size);
            BitmapImage image;

            if (bytes != null)
            {
                var extracted = CreateBitmapImage(bytes, size);
                if (extracted != null)
                {
                    image = extracted;

                    // 写入文件缓存（尽力而为）
                    try
                    {
                        Directory.CreateDirectory(CacheDir);
                        File.WriteAllBytes(cacheFile, bytes);
                    }
                    catch
                    {
                        // 磁盘写入失败不影响内存缓存
                    }

                    Interlocked.Increment(ref _extractionSuccess);
                }
                else
                {
                    // PNG → BitmapImage 转换失败
                    image = GetDefaultIcon(size);
                    Interlocked.Increment(ref _extractionFail);
                }
            }
            else
            {
                // Win32 图标提取失败，使用默认图标降级
                image = GetDefaultIcon(size);
                Interlocked.Increment(ref _extractionFail);
                Debug.WriteLine($"[AppIconExtractor] 图标提取失败，使用默认图标（累计失败数：{_extractionFail}）");
            }

            SetMemoryCache(cacheKey, image);
            return image;
        });
    }

    /// <summary>
    /// 批量预加载多个 exe 文件的图标（并行提取）。
    /// 自动过滤网络路径和不存在的文件。
    /// </summary>
    /// <param name="exePaths">exe 路径集合</param>
    /// <param name="size">目标图标尺寸，默认 32</param>
    /// <returns>路径到 BitmapImage 的映射字典</returns>
    public async Task<Dictionary<string, BitmapImage>> ExtractIconsAsync(
        IEnumerable<string> exePaths, int size = 32)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AppIconExtractor));

        var pathList = exePaths.ToList();
        var results = new Dictionary<string, BitmapImage>();

        // 并行提取，自动过滤非法路径
        var tasks = pathList
            .Where(p => IsLocalPath(p) && File.Exists(p))
            .Select(async p =>
            {
                try
                {
                    var icon = await ExtractIconAsync(p, size);
                    return (path: p, icon);
                }
                catch
                {
                    return (path: p, icon: GetDefaultIcon(size));
                }
            });

        var completed = await Task.WhenAll(tasks);
        foreach (var (path, icon) in completed)
            results[path] = icon;

        return results;
    }

    /// <summary>
    /// 清理缓存：释放内存缓存中已失效的弱引用，删除超过 7 天未访问的磁盘缓存文件。
    /// </summary>
    public void CleanupCache()
    {
        // 清理内存缓存中的死引用
        int removedCount;
        lock (_cacheLock)
        {
            var deadKeys = _memoryCache
                .Where(kvp => !kvp.Value.TryGetTarget(out _))
                .Select(kvp => kvp.Key)
                .ToList();
            removedCount = deadKeys.Count;
            foreach (var key in deadKeys)
                _memoryCache.Remove(key);
        }

        // 清理过期磁盘缓存
        int fileRemoved = 0;
        try
        {
            if (!Directory.Exists(CacheDir))
                return;

            var cutoff = DateTime.Now - CacheExpiry;
            foreach (var file in Directory.EnumerateFiles(CacheDir, "*.png"))
            {
                try
                {
                    var lastAccess = File.GetLastAccessTime(file);
                    if (lastAccess < cutoff)
                    {
                        File.Delete(file);
                        fileRemoved++;
                    }
                }
                catch
                {
                    // 单个文件清理失败不影响其他文件
                }
            }
        }
        catch
        {
            // 磁盘清理失败不影响运行
        }

        Debug.WriteLine($"[AppIconExtractor] 缓存清理完成：释放内存条目 {removedCount}，删除过期文件 {fileRemoved}");
    }

    /// <summary>
    /// 释放资源，清理内存缓存。磁盘缓存文件不删除（由 CleanupCache 管理）。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_cacheLock)
        {
            _memoryCache.Clear();
        }

        _disposed = true;
    }

    /// <summary>
    /// 获取缓存统计信息（用于性能监控，不含路径信息）。
    /// </summary>
    /// <returns>(总请求数, 缓存命中数, 提取成功数, 提取失败数)</returns>
    public (int totalRequests, int cacheHits, int extractionSuccess, int extractionFail)
        GetStats() => (_totalRequests, _cacheHits, _extractionSuccess, _extractionFail);

    /// <summary>
    /// 输出缓存统计信息到调试输出（不含路径信息，仅数量统计）。
    /// </summary>
    [Conditional("DEBUG")]
    public void LogStats()
    {
        var (total, hits, success, fail) = GetStats();
        var hitRate = total > 0 ? (double)hits / total * 100 : 0;
        Debug.WriteLine($"[AppIconExtractor] 统计：总请求 {total}，缓存命中 {hits}（{hitRate:F1}%），提取成功 {success}，提取失败 {fail}");
    }

    // ===== 隐私保护 =====

    /// <summary>
    /// 验证路径是否为本地磁盘路径（拒绝 UNC 网络路径如 \\server\share）。
    /// 所有图标提取操作仅限于本地，无任何网络请求。
    /// </summary>
    private static bool IsLocalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // 快速拒绝 UNC 路径（\\server\share）
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
            return false;

        // 使用 Uri 严格验证：必须是本地文件路径且非 UNC
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
            return uri.IsFile && !uri.IsUnc;

        // 非 URI 格式路径（如 C:\...），检查盘符格式 X:\
        return path.Length >= 3 &&
               char.IsLetter(path[0]) &&
               path[1] == ':' &&
               (path[2] == '\\' || path[2] == '/');
    }

    // ===== 缓存管理 =====

    /// <summary>
    /// 生成缓存键：SHA256(exePath + "|" + size) 的十六进制字符串。
    /// 使用哈希而非明文路径，防止缓存文件名泄露用户安装的应用列表。
    /// </summary>
    private static string GetCacheKey(string exePath, int size)
    {
        var input = $"{exePath}|{size}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>获取缓存文件完整路径</summary>
    private static string GetCacheFilePath(string cacheKey)
        => Path.Combine(CacheDir, $"{cacheKey}.png");

    /// <summary>从内存缓存中获取图标（线程安全）</summary>
    private bool TryGetMemoryCache(string key, out BitmapImage? image)
    {
        lock (_cacheLock)
        {
            image = null;
            if (_memoryCache.TryGetValue(key, out var weakRef))
            {
                if (weakRef.TryGetTarget(out var target))
                {
                    image = target;
                    return true;
                }
                // 弱引用已失效（目标已被 GC 回收），移除条目
                _memoryCache.Remove(key);
            }
            return false;
        }
    }

    /// <summary>写入内存缓存（线程安全）</summary>
    private void SetMemoryCache(string key, BitmapImage image)
    {
        lock (_cacheLock)
        {
            _memoryCache[key] = new WeakReference<BitmapImage>(image);
        }
    }

    // ===== 图标提取 =====

    /// <summary>
    /// 提取图标并转为 PNG 字节数组。
    /// 优先使用 SHGetFileInfo 获取大图标（更好的画质，优先最大可用尺寸），
    /// 失败时降级到 ExtractAssociatedIcon。
    /// </summary>
    private byte[]? ExtractIconBytes(string exePath, int size)
    {
        // 尝试 1：SHGetFileInfo 获取大图标
        var hIcon = GetHIconViaSHGetFileInfo(exePath);

        // 尝试 2：ExtractAssociatedIcon 降级
        if (hIcon == IntPtr.Zero)
            hIcon = GetHIconViaExtractAssociatedIcon(exePath);

        if (hIcon == IntPtr.Zero)
            return null;

        try
        {
            // 转换链：HICON → Icon.FromHandle → Bitmap → 缩放 → PNG
            using var icon = Icon.FromHandle(hIcon);
            using var sourceBitmap = icon.ToBitmap();
            using var resized = ResizeBitmap(sourceBitmap, size, size);
            using var ms = new MemoryStream();
            resized.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
        finally
        {
            // Icon.FromHandle 不接管 HICON 所有权，必须手动 DestroyIcon 防止 GDI 泄漏
            DestroyIcon(hIcon);
        }
    }

    /// <summary>通过 SHGetFileInfo 获取大图标 HICON</summary>
    private static IntPtr GetHIconViaSHGetFileInfo(string exePath)
    {
        try
        {
            var shfi = new SHFILEINFO();
            SHGetFileInfo(exePath, 0, ref shfi,
                (uint)Marshal.SizeOf<SHFILEINFO>(),
                SHGFI_ICON | SHGFI_LARGEICON);
            return shfi.hIcon;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 通过 ExtractAssociatedIcon 获取 HICON。
    /// 使用 StringBuilder 预留空间，因为该函数会修改路径参数（追加图标索引）。
    /// </summary>
    private static IntPtr GetHIconViaExtractAssociatedIcon(string exePath)
    {
        try
        {
            var pathBuilder = new StringBuilder(exePath, exePath.Length + 16);
            int iconIndex = 0;
            return ExtractAssociatedIcon(IntPtr.Zero, pathBuilder, ref iconIndex);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    // ===== 图像处理 =====

    /// <summary>
    /// 高质量缩放 Bitmap 到目标尺寸（使用双三次插值）。
    /// </summary>
    private static Bitmap ResizeBitmap(Bitmap source, int width, int height)
    {
        var result = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        result.SetResolution(source.HorizontalResolution, source.VerticalResolution);

        using (var g = Graphics.FromImage(result))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(source, 0, 0, width, height);
        }

        return result;
    }

    /// <summary>
    /// 从 PNG 字节数组创建 WPF BitmapImage（已 Freeze，可跨线程使用）。
    /// </summary>
    private static BitmapImage? CreateBitmapImage(byte[] pngBytes, int decodeSize)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = decodeSize;
            image.DecodePixelHeight = decodeSize;
            using var ms = new MemoryStream(pngBytes);
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze(); // 使其可跨线程访问
            return image;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取默认应用图标（SystemIcons.Application）。
    /// 提取失败时的降级方案，确保 UI 始终有图标可显示。
    /// </summary>
    private static BitmapImage GetDefaultIcon(int size)
    {
        try
        {
            using var bitmap = SystemIcons.Application.ToBitmap();
            using var resized = ResizeBitmap(bitmap, size, size);
            using var ms = new MemoryStream();
            resized.Save(ms, ImageFormat.Png);
            return CreateBitmapImage(ms.ToArray(), size)!;
        }
        catch
        {
            // 极端情况：SystemIcons 也不可用，返回空白图标
            return CreateEmptyImage();
        }
    }

    /// <summary>创建空白 BitmapImage（最终降级方案）</summary>
    private static BitmapImage CreateEmptyImage()
    {
        var image = new BitmapImage();
        image.Freeze();
        return image;
    }
}
