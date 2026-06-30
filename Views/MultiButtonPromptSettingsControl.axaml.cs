using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Shared.Models.Automation;
using FluentAvalonia.UI.Controls;
using InquiryWindow.Models;
using InquiryWindow.Services;

namespace InquiryWindow.Views;

public partial class MultiButtonPromptSettingsControl : ActionSettingsControlBase<MultiButtonPromptSettings>
{
    public MultiButtonPromptSettingsControl()
    {
        InitializeComponent();
    }

    private void OnAddButtonClick(object? sender, RoutedEventArgs e)
    {
        Settings.Buttons.Add(new MultiButtonPromptButton
        {
            Name = "按钮 " + (Settings.Buttons.Count + 1),
            Icon = "\uE10F"
        });
    }

    private async void OnInsertPresetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: MultiButtonPromptButton target }) return;

        var store = PresetsStore.Instance;
        // 确保预设库已加载
        store.Load();

        if (store.Presets.Count == 0)
        {
            await ShowEmptyDialogAsync();
            return;
        }

        var items = store.Presets
            .Select(p => new MenuFlyoutItem
            {
                Text = p.Name,
                CommandParameter = p,
                Tag = target
            })
            .ToList();

        var flyout = new MenuFlyout();
        foreach (var item in items)
        {
            item.Click += OnPresetItemClick;
            flyout.Items.Add(item);
        }

        if (sender is Control c)
        {
            flyout.ShowAt(c);
        }
    }

    private void OnPresetItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { CommandParameter: ButtonPreset preset, Tag: MultiButtonPromptButton target })
            return;

        // 深拷贝 Action 链（避免共享引用）
        foreach (var item in preset.Actions.ActionItems)
        {
            target.Actions.ActionItems.Add(CloneItem(item));
        }
    }

    private static ActionItem CloneItem(ActionItem item)
    {
        // 简单深拷贝：Id 和 Settings 一起搬过去。
        // ClassIsland 的 ActionService 每次 invoke 时会按 id 重新实例化 ActionBase，
        // Settings 在 invoke 时也会重新反序列化，所以共享 Settings 引用是安全的。
        return new ActionItem
        {
            Id = item.Id,
            Settings = item.Settings
        };
    }

    private static async Task ShowEmptyDialogAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "没有可用的预设",
            Content = "请到插件设置里先添加按钮预设。",
            PrimaryButtonText = "确定",
            DefaultButton = ContentDialogButton.Primary
        };
        await dialog.ShowAsync();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
