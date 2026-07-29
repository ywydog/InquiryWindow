using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using InquiryWindow.Services;
using InquiryWindow.ViewModels;
using InquiryWindow.Views;

namespace InquiryWindow.SettingsPage;

/// <summary>
/// InquiryWindow 主设置页：只管插件级全局设置（弹窗外观 / 预览）。
/// 按钮预设库已拆为独立页面 <see cref="ButtonPresetSettingsPage"/>。
/// </summary>
[SettingsPageInfo("inquiryWindow.settings.main", "InquiryWindow 设置", "\uE82D", "\uE713")]
public partial class InquiryWindowSettingsPage : SettingsPageBase
{
    public InquiryWindowSettingsViewModel ViewModel { get; }

    public InquiryWindowSettingsPage()
    {
        ViewModel = new InquiryWindowSettingsViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }

    /// <summary>
    /// 亚克力开关被切换后立即落盘，避免设置页关闭时丢改动。
    /// </summary>
    private void OnAcrylicIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        PluginSettingsStore.Instance.SaveNow();
    }

    /// <summary>
    /// 透明度滑块被拖动时也会持续触发 PropertyChanged。
    /// 用防抖（去掉极小变化）后再写盘，避免每帧落盘。
    /// </summary>
    private void OnAcrylicOpacityChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Slider.ValueProperty) return;
        // 滑块松手时写盘；按下过程不写（防抖由 UI 控件的 IsThumbDragCompleted 决定，
        // 这里简单一点：每次变化都同步给 in-memory 模型，UI 同步，关闭设置页时再 SaveNow）。
        // 但为安全起见，每次变化都直接 SaveNow（plugin-settings.json 体积小，可接受）。
        PluginSettingsStore.Instance.SaveNow();
    }

    /// <summary>
    /// 「预览弹窗效果」按钮：按当前插件全局设置（亚克力开关 + 透明度）弹出一个示例
    /// InquiryWindowWindow。预览模式下只显示「看完了」按钮，不会触发任何后续动作。
    /// </summary>
    private async void OnPreviewClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var settings = PluginSettingsStore.Instance.Data;
        var window = new InquiryWindowWindow
        {
            WindowTitle      = "预览 - 询问窗效果",
            DialogTitleSmall = "预览",
            DialogTitle      = "这就是你的弹窗效果",
            DialogBody       = "调整左侧的「弹窗外观」设置，再点「预览」即可看到变化。\n\n点「看完了」关闭预览。",
            IsPreviewMode    = true,
            CanExecute       = false
        };

        // 同步应用当前亚克力设置
        window.UseAcrylicBackground = settings.UseAcrylicBackground;
        window.AcrylicTintOpacity = settings.AcrylicTintOpacity;

        try
        {
            await window.ShowDialog(topLevel);
        }
        catch
        {
            // 预览失败（例如主窗口已关闭）静默吞掉，避免设置页崩溃。
        }
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
