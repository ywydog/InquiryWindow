using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using InquiryWindow.Services;
using InquiryWindow.ViewModels;

namespace InquiryWindow.SettingsPage;

/// <summary>
/// 按钮预设库设置页：
/// 借鉴 SuperAutoIsland 「可复用行动组」设计理念 —— 左列表 / 右详情双栏布局，
/// 配合 SystemTools 多设置页面注册风格独立成一个 SettingsPage。
/// </summary>
[SettingsPageInfo("inquiryWindow.settings.presets",
    "InquiryWindow - 按钮预设库",
    "\uE82D",
    "\uE82D")]
public partial class ButtonPresetSettingsPage : SettingsPageBase
{
    public ButtonPresetSettingsViewModel ViewModel { get; }

    public ButtonPresetSettingsPage()
    {
        ViewModel = new ButtonPresetSettingsViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }

    /// <summary>
    /// 「删除」按钮：把当前 TopLevel 透传给 ViewModel，避免在 VM 里直接依赖 UI 类型。
    /// </summary>
    private async void OnRemovePresetClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        // 注：不先调用 CanExecute —— Android AOT 下 IRelayCommand<T>.CanExecute 无法解析，
        // 命令内部自带 null 保护，直接执行即可。
        await ViewModel.RemovePresetCommand.ExecuteAsync(topLevel);
    }

    /// <summary>
    /// 页面从可视树卸载时释放 ViewModel 对静态 PresetsStore 的订阅，避免泄漏。
    /// 同时立即落盘一次：Action 链是就地编辑的，没有「保存」按钮兜底。
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ViewModel.Dispose();
        PresetsStore.Instance.SaveNow();
    }
}
