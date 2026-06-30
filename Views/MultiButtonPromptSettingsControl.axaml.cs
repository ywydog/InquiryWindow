using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Shared.Helpers;
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
        store.Load();

        if (store.Presets.Count == 0)
        {
            await ShowEmptyDialogAsync();
            return;
        }

        // 把每个 item 的引用先存起来，等 flyout 关闭时统一解绑 Click，
        // 避免频繁开 flyout 时事件订阅累计。
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

        flyout.Closing += (_, _) =>
        {
            foreach (var item in items)
            {
                item.Click -= OnPresetItemClick;
            }
        };

        flyout.ShowAt(sender);
    }

    private void OnPresetItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { CommandParameter: ButtonPreset preset, Tag: MultiButtonPromptButton target })
            return;

        // 深拷贝整条 Action 链（避免和预设共享引用，导致改一处影响全部）
        var clone = ConfigureFileHelper.CopyObject(preset.Actions);
        if (clone is null)
        {
            // 极少见：配置损坏 / CopyObject 不支持该类型
            return;
        }
        foreach (var item in clone.ActionItems)
        {
            target.Actions.ActionItems.Add(item);
        }
    }

    private static async Task ShowEmptyDialogAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "没有可用的预设",
            Content = "请到插件设置（InquiryWindow 设置 → 按钮预设库）里先添加按钮预设。",
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
