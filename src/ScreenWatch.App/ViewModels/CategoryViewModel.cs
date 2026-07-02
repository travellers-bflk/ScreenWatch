using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenWatch.App.Services;
using ScreenWatch.Core.Models;

namespace ScreenWatch.App.ViewModels;

/// <summary>
/// 分类行数据（支持 UI 双向绑定）。
/// </summary>
public class CategoryRow : ObservableObject
{
    public int CategoryId { get; init; }

    private string _name = "";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private string _color = "#2196F3";
    public string Color
    {
        get => _color;
        set
        {
            if (SetProperty(ref _color, value))
                OnPropertyChanged(nameof(ColorBrush));
        }
    }

    private string _icon = "";
    /// <summary>分类 Emoji 图标（如 💼）。用户自建分类可能为空。</summary>
    public string Icon
    {
        get => _icon;
        set
        {
            if (SetProperty(ref _icon, value))
                OnPropertyChanged(nameof(IconDisplay));
        }
    }

    /// <summary>显示用图标：有图标时返回图标，无图标时返回默认标签 emoji</summary>
    public string IconDisplay => string.IsNullOrWhiteSpace(Icon) ? "🏷️" : Icon;

    /// <summary>将颜色 hex 字符串转换为 SolidColorBrush</summary>
    public SolidColorBrush ColorBrush
    {
        get
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(
                    string.IsNullOrEmpty(Color) ? "#2196F3" : Color);
                return new SolidColorBrush(color);
            }
            catch
            {
                return new SolidColorBrush(Colors.SteelBlue);
            }
        }
    }
}

/// <summary>
/// 未归类应用行数据。
/// </summary>
public class UnrecognizedAppRow : ObservableObject
{
    public int AppId { get; init; }
    public string DisplayName { get; init; } = "";
    public string ExeName { get; init; } = "";

    private int? _selectedCategoryId;
    public int? SelectedCategoryId
    {
        get => _selectedCategoryId;
        set => SetProperty(ref _selectedCategoryId, value);
    }
}

/// <summary>
/// 分类管理页视图模型：分类增删改 + 未归类应用归类。
/// </summary>
public partial class CategoryViewModel : ObservableObject
{
    // ----- 分类列表 -----
    public ObservableCollection<CategoryRow> Categories { get; } = new();

    [ObservableProperty] private string _newCategoryName = "";
    [ObservableProperty] private string _newCategoryColor = "#2196F3";

    // ----- 未归类应用 -----
    public ObservableCollection<UnrecognizedAppRow> UnrecognizedApps { get; } = new();

    /// <summary>用于批量归类的分类下拉</summary>
    [ObservableProperty] private int? _batchCategoryId;

    /// <summary>未归类应用数量</summary>
    [ObservableProperty] private int _unrecognizedCount;

    // 预设颜色列表
    public string[] PresetColors { get; } =
    {
        "#2196F3", "#4CAF50", "#FF9800", "#F44336", "#9C27B0",
        "#00BCD4", "#795548", "#607D8B", "#E91E63", "#8BC34A"
    };

    /// <summary>
    /// 加载分类和未归类应用数据。
    /// </summary>
    public void LoadData()
    {
        LoadCategories();
        LoadUnrecognizedApps();
    }

    private void LoadCategories()
    {
        Categories.Clear();
        foreach (var cat in ServiceHost.CategoryRepository.GetAll())
        {
            Categories.Add(new CategoryRow
            {
                CategoryId = cat.CategoryId,
                Name = cat.Name,
                Color = string.IsNullOrEmpty(cat.Color) ? "#2196F3" : cat.Color,
                Icon = cat.Icon ?? ""
            });
        }
    }

    private void LoadUnrecognizedApps()
    {
        UnrecognizedApps.Clear();
        foreach (var app in ServiceHost.AppRepository.GetUnrecognizedApps())
        {
            UnrecognizedApps.Add(new UnrecognizedAppRow
            {
                AppId = app.AppId,
                DisplayName = string.IsNullOrEmpty(app.DisplayName) ? app.ExeName : app.DisplayName,
                ExeName = app.ExeName
            });
        }
        UnrecognizedCount = UnrecognizedApps.Count;
    }

    // ================================================================
    //  分类管理命令
    // ================================================================

    /// <summary>
    /// 添加新分类。
    /// </summary>
    [RelayCommand]
    private void AddCategory()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
            return;

        ServiceHost.CategoryRepository.Add(NewCategoryName.Trim(), NewCategoryColor);
        NewCategoryName = "";
        LoadCategories();
    }

    /// <summary>
    /// 保存分类修改。
    /// </summary>
    [RelayCommand]
    private void SaveCategory(CategoryRow row)
    {
        if (row == null) return;
        ServiceHost.CategoryRepository.Update(row.CategoryId, row.Name, row.Color);
        LoadCategories();
        // 分类名称可能变化，刷新未归类应用
        LoadUnrecognizedApps();
    }

    /// <summary>
    /// 删除分类。
    /// </summary>
    [RelayCommand]
    private void DeleteCategory(CategoryRow row)
    {
        if (row == null) return;
        ServiceHost.CategoryRepository.Delete(row.CategoryId);
        LoadCategories();
    }

    // ================================================================
    //  未归类应用归类命令
    // ================================================================

    /// <summary>
    /// 一键将单个未归类应用归入所选分类（由 ComboBox SelectionChanged 触发）。
    /// </summary>
    public void SaveAppCategory(UnrecognizedAppRow row)
    {
        if (row == null || !row.SelectedCategoryId.HasValue)
            return;

        ServiceHost.AppRepository.UpdateCategory(row.AppId, row.SelectedCategoryId.Value);
        LoadUnrecognizedApps();
    }

    /// <summary>
    /// 对所有未归类应用执行关键词自动分类。返回被分类的应用数量。
    /// </summary>
    public int ExecuteAutoCategorizeAll()
    {
        var count = ServiceHost.ClassificationService.AutoCategorizeAllApps();
        LoadData();
        return count;
    }

    /// <summary>
    /// 重置全部分类并重新自动分类。返回被分类的应用数量。
    /// </summary>
    public int ExecuteResetAllCategories()
    {
        var count = ServiceHost.ClassificationService.ResetAllCategories();
        LoadData();
        return count;
    }

    /// <summary>
    /// 批量将所有未归类应用归入所选分类。
    /// </summary>
    [RelayCommand]
    private void BatchAssign()
    {
        if (!BatchCategoryId.HasValue || UnrecognizedApps.Count == 0)
            return;

        foreach (var app in UnrecognizedApps)
        {
            ServiceHost.AppRepository.UpdateCategory(app.AppId, BatchCategoryId.Value);
        }

        BatchCategoryId = null;
        LoadUnrecognizedApps();
    }
}
