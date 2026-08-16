using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Controls;
using InquiryWindow.Models;

namespace InquiryWindow.Views;

public partial class InquiryWindowSettingsControl : ActionSettingsControlBase<InquiryWindowActionSettings>
{
    /// <summary>
    /// 公开暴露设置对象，供 Avalonia 编译型绑定访问（基类的 Settings 是 protected，编译型
    /// 绑定无法访问，会导致 XAML 中 {Binding Settings.XXX} 全部失效）。
    /// </summary>
    public new InquiryWindowActionSettings Settings => base.Settings;

    public InquiryWindowSettingsControl()
    {
        InitializeComponent();
    }

    private async void OnBrowseFileClick(object? sender, RoutedEventArgs e)
    {
        await PickAsync(async topLevel =>
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择目标文件或程序",
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                var path = files[0].TryGetLocalPath();
                if (!string.IsNullOrEmpty(path))
                {
                    Settings.TargetPath = path;
                }
            }
        });
    }

    private async void OnBrowseFolderClick(object? sender, RoutedEventArgs e)
    {
        await PickAsync(async topLevel =>
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择目标文件夹",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                var path = folders[0].TryGetLocalPath();
                if (!string.IsNullOrEmpty(path))
                {
                    Settings.TargetPath = path;
                }
            }
        });
    }

    private async void OnPreviewBodyClick(object? sender, RoutedEventArgs e)
    {
        // 预览完整的询问窗弹窗（预览模式：只显示「看完了」按钮，不触发执行）。
        var window = new InquiryWindowWindow
        {
            WindowTitle = Settings.WindowTitle,
            DialogTitleSmall = Settings.DialogTitle,
            DialogTitle = Settings.DialogTitle,
            DialogBody = Settings.DialogBody ?? "",
            PathText = Settings.TargetPath,
            IsPathVisible = Settings.ShowPath && !string.IsNullOrWhiteSpace(Settings.TargetPath),
            CanExecute = !string.IsNullOrWhiteSpace(Settings.TargetPath),
            IsPreviewMode = true
        };
        await window.ShowDialog(AppBase.Current.GetRootWindow() as Window);
    }

    private async Task PickAsync(Func<TopLevel, Task> picker)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        try
        {
            await picker(topLevel);
        }
        catch (Exception ex)
        {
            await CommonTaskDialogs.ShowDialog("选择失败", $"无法打开系统选择器：{ex.Message}");
        }
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
