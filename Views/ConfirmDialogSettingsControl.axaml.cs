using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClassIsland.Core.Abstractions.Controls;
using ConfirmDialogAction.Models;

namespace ConfirmDialogAction.Views;

public partial class ConfirmDialogSettingsControl : ActionSettingsControlBase<ConfirmDialogActionSettings>
{
    public ConfirmDialogSettingsControl()
    {
        InitializeComponent();
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        // 弹出系统文件选择器，获取本地路径后写回设置。
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

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
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
