using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Controls.Automation;
using FluentAvalonia.UI.Controls;
using InquiryWindow.Models;
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
            ViewModel.RemovePresetCommand.Execute(preset);
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
            Header = "名称",
            Text = preset.Name
        };
        var iconBox = new TextBox
        {
            Header = "图标字符（Fluent SystemIcons）",
            Text = preset.Icon,
            Width = 120,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };
        var actionControl = new ActionControl { ActionSet = preset.Actions };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(nameBox);
        content.Children.Add(iconBox);
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
        if (result == ContentDialogResult.Primary)
        {
            preset.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? "未命名预设" : nameBox.Text;
            preset.Icon = string.IsNullOrWhiteSpace(iconBox.Text) ? "\uE10F" : iconBox.Text;
        }
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
