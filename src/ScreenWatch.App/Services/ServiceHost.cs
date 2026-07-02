using ScreenWatch.App.Services;
using ScreenWatch.Core.Data;
using ScreenWatch.Core.Services;

namespace ScreenWatch.App.Services;

/// <summary>
/// 全局服务定位器，在应用启动时初始化所有共享的 Repository 和查询服务实例。
/// 所有 ViewModel 通过此类访问后端服务。
/// </summary>
public static class ServiceHost
{
    /// <summary>数据库管理器单例</summary>
    public static DatabaseManager Database { get; private set; } = null!;

    /// <summary>应用仓库</summary>
    public static IAppRepository AppRepository { get; private set; } = null!;

    /// <summary>会话仓库</summary>
    public static ISessionRepository SessionRepository { get; private set; } = null!;

    /// <summary>分类仓库</summary>
    public static ICategoryRepository CategoryRepository { get; private set; } = null!;

    /// <summary>时段仓库</summary>
    public static ITimePeriodRepository TimePeriodRepository { get; private set; } = null!;

    /// <summary>排除规则仓库</summary>
    public static IExclusionRepository ExclusionRepository { get; private set; } = null!;

    /// <summary>设置仓库</summary>
    public static ISettingsRepository SettingsRepository { get; private set; } = null!;

    /// <summary>使用统计查询服务</summary>
    public static IUsageQueryService UsageQueryService { get; private set; } = null!;

    /// <summary>应用自动分类服务（关键词匹配 + 重置）</summary>
    public static AppClassificationService ClassificationService { get; private set; } = null!;

    /// <summary>开机自启服务</summary>
    public static AutoStartService AutoStartService { get; private set; } = null!;

    /// <summary>
    /// 初始化所有服务实例。在 App.OnStartup 中调用。
    /// </summary>
    public static void Initialize()
    {
        var db = DatabaseManager.Instance;
        db.Initialize();

        Database = db;
        AppRepository = new AppRepository(db);
        SessionRepository = new SessionRepository(db);
        CategoryRepository = new CategoryRepository(db);
        TimePeriodRepository = new TimePeriodRepository(db);
        ExclusionRepository = new ExclusionRepository(db);
        SettingsRepository = new SettingsRepository(db);

        UsageQueryService = new UsageQueryService(
            SessionRepository, AppRepository, CategoryRepository, TimePeriodRepository);

        ClassificationService = new AppClassificationService(
            AppRepository, CategoryRepository, new BuiltinRulesService());

        AutoStartService = new AutoStartService();
    }
}
