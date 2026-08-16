using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
        // 弹一个独立弹窗预览 Markdown 渲染效果（用户点按钮才看，不是实时）。
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        await MarkdownPreviewDialog.ShowAsync(
            topLevel,
            title: "弹窗正文预览",
            markdown: Settings.DialogBody ?? "");
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
