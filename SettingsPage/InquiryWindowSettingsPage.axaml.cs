using Avalonia.Controls;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using InquiryWindow.ViewModels;

namespace InquiryWindow.SettingsPage;

/// <summary>
/// InquiryWindow 主设置页。
///
/// Android 兼容说明（new/for-android-2.2 分支专用）：
/// 移除了亚克力背景相关的保存入口和预览功能（WindowTransparencyLevel.AcrylicBlur 在 Android 上不支持）。
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

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
