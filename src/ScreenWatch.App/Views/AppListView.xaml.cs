using System.Windows;
using System.Windows.Controls;
using ScreenWatch.App.ViewModels;

namespace ScreenWatch.App.Views;

/// <summary>
/// 应用列表页：展示所有应用及其使用时长，支持修改分类。
/// </summary>
public partial class AppListView : UserControl
{
    public AppListView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// ComboBox 选择变更时立即保存分类（一键完成，无需点"应用"按钮）。
    /// 通过 RemovedItems.Count == 0 过滤初始绑定触发的事件。
    /// </summary>
    private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 初始绑定（无移除项）时跳过，避免加载时触发多余保存
        if (e.RemovedItems.Count == 0)
            return;

        if (sender is not ComboBox comboBox)
            return;
        if (comboBox.DataContext is not AppListRow row)
            return;
        if (DataContext is not AppListViewModel vm)
            return;

        // 确保 row.CategoryId 已从 ComboBox 选择更新
        if (comboBox.SelectedValue is int categoryId)
            row.CategoryId = categoryId;

        vm.SaveAppCategory(row);
    }

    /// <summary>
    /// 对所有未归类应用执行关键词自动分类。
    /// </summary>
    private void OnAutoCategorizeClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AppListViewModel vm)
            return;

        var count = vm.ExecuteAutoCategorizeAll();

        MessageBox.Show(
            count > 0
                ? $"已自动分类 {count} 个应用。"
                : "没有需要自动分类的未归类应用。",
            "自动分类", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// 重置全部分类并重新自动分类（带确认对话框）。
    /// </summary>
    private void OnResetAllCategoriesClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AppListViewModel vm)
            return;

        var result = MessageBox.Show(
            "确定要重置所有应用的分类吗？\n这将清除所有分类并重新自动分类。",
            "确认重置", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        var count = vm.ExecuteResetAllCategories();

        MessageBox.Show(
            $"已重置并重新分类 {count} 个应用。",
            "重置完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
