using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
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
    /// 「选择图标…」按钮：把当前 TopLevel 透传给 ViewModel，避免在 VM 里直接依赖 UI 类型。
    /// </summary>
    private async void OnPickIconClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        // 注：不通过 RelayCommand —— Android AOT 会裁剪 IAsyncRelayCommand<T>.ExecuteAsync(T)
        // 与 CanExecute(T)，直接调用 ViewModel 暴露的公开方法即可（方法内部自带 null 保护）。
        await ViewModel.PickIconAsync(topLevel);
    }

    /// <summary>
    /// 「编辑 Action 链…」按钮：把右侧详情里点出的弹窗交由 VM 调度。
    /// </summary>
    private async void OnEditActionsClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        // 注：同上，直接调用公开方法，避开被 AOT 裁剪的命令接口。
        await ViewModel.BeginEditActionsAsync(topLevel);
    }

    /// <summary>
    /// 「删除」按钮：把当前 TopLevel 透传给 ViewModel，避免在 VM 里直接依赖 UI 类型。
    /// </summary>
    private async void OnRemovePresetClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        // 注：同上，直接调用公开方法，避开被 AOT 裁剪的命令接口。
        await ViewModel.RemovePresetAsync(topLevel);
    }

    /// <summary>
    /// 页面从可视树卸载时释放 ViewModel 对静态 PresetsStore 的订阅，避免泄漏。
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ViewModel.Dispose();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
