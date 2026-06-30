using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls.Automation;
using ClassIsland.Shared.Helpers;
using FluentAvalonia.UI.Controls;
using InquiryWindow.Models;
using InquiryWindow.Services;
using InquiryWindow.ViewModels;

namespace InquiryWindow.SettingsPage;

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

    private void OnAddPresetClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddPresetCommand.Execute(null);
    }

    private async void OnRemovePresetClick(object? sender, RoutedEventArgs e)
    {
        var preset = ViewModel.SelectedPreset;
        if (preset == null) return;

        var dialog = new ContentDialog
        {
            Title = "删除预设？",
            Content = $"确定删除预设「{preset.Name}」？已插入到按钮里的 Action 链不受影响。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            PresetsStore.Instance.RemovePreset(preset);
        }
    }

    private async void OnEditPresetClick(object? sender, RoutedEventArgs e)
    {
        var preset = ViewModel.SelectedPreset;
        if (preset == null) return;

        await EditPresetAsync(preset);
    }

    private async Task EditPresetAsync(ButtonPreset preset)
    {
        var nameBox = new TextBox
        {
            Text = preset.Name
        };
        var iconBox = new TextBox
        {
            Text = preset.Icon,
            Width = 120,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };

        // 关键：克隆一份 ActionSet 给 ActionControl 编辑，
        // 这样「取消」时用户的改动会随 workingActions 一起被丢弃，preset.Actions 不被污染。
        var workingActions = ConfigureFileHelper.CopyObject(preset.Actions);
        var actionControl = new ActionControl { ActionSet = workingActions };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(MakeLabeled("名称", nameBox));
        content.Children.Add(MakeLabeled("图标字符（Fluent SystemIcons）", iconBox));
        content.Children.Add(new TextBlock
        {
            Text = "Action 链：",
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        content.Children.Add(actionControl);

        var dialog = new ContentDialog
        {
            Title = "编辑预设",
            Content = content,
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        preset.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? "未命名预设" : nameBox.Text;
        preset.Icon = string.IsNullOrWhiteSpace(iconBox.Text) ? "\uE10F" : iconBox.Text;
        preset.Actions = workingActions;

        // Name/Icon/Actions 的赋值都会通过 INPC 触发 PresetsStore 的 debounce save，
        // 但 debounce 400ms 后才落盘。显式同步 SaveNow 兜底，避免用户编辑完立刻关 app 丢数据。
        PresetsStore.Instance.SaveNow();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private static StackPanel MakeLabeled(string label, Control content)
    {
        // Avalonia 11.0 的 TextBox 没有 Header 属性，所以用 TextBlock 标签 + 控件的组合代替。
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        sp.Children.Add(content);
        return sp;
    }
}
