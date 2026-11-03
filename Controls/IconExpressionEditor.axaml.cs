using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using InquiryWindow.Models.UI;

namespace InquiryWindow.Controls;

/// <summary>
/// 图标表达式编辑器：按钮上显示当前图标预览，点击后在 <see cref="Flyout"/> 中选择图标。
///
/// <para>
/// 支持 Fluent 字体图标、Lucide 字体图标与图片三种来源，最终都回吐为宿主的图标表达式
/// （<c>fluent("…")</c> / <c>lucide("…")</c> / <c>img("…")</c>），可直接交给
/// <c>IconExpressionHelper</c> 解析渲染。
/// </para>
/// </summary>
public partial class IconExpressionEditor : UserControl
{
    /// <summary>
    /// 当前图标表达式。双向绑定，改变时会同步刷新预览与编辑器状态。
    /// </summary>
    public static readonly StyledProperty<string?> IconExpressionProperty =
        AvaloniaProperty.Register<IconExpressionEditor, string?>(
            nameof(IconExpression), "",
            defaultBindingMode: BindingMode.TwoWay);

    public string? IconExpression
    {
        get => GetValue(IconExpressionProperty);
        set => SetValue(IconExpressionProperty, value);
    }

    private readonly IconExpressionEditorViewModel _viewModel = new();

    private Flyout EditorFlyout => (Flyout)EditorButton.Flyout!;

    public IconExpressionEditor()
    {
        InitializeComponent();
        EditorRoot.DataContext = FlyoutRoot.DataContext = _viewModel;
        _viewModel.ExpressionEdited += expression => SetCurrentValue(IconExpressionProperty, expression);
        _viewModel.PropertyChanged += OnEditorPropertyChanged;
        _viewModel.ApplyExpression(IconExpression);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconExpressionProperty)
        {
            _viewModel.ApplyExpression(change.GetNewValue<string?>());
        }
    }

    private void OnFlyoutOpened(object? sender, EventArgs e) => _viewModel.Activate();

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 筛选结果变化后把列表滚回顶部，避免停留在上一次的滚动位置。
        if (e.PropertyName == nameof(IconExpressionEditorViewModel.FilteredIcons))
        {
            IconScrollViewer.Offset = default;
        }
    }

    private void OnIconClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: IconExpressionEditorItem item })
        {
            _viewModel.SelectIcon(item);
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Down || _viewModel.FilteredIcons.Count == 0)
        {
            return;
        }

        FocusIcon(0);
        e.Handled = true;
    }

    private void OnIconKeyDown(object? sender, KeyEventArgs e)
    {
        var button = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<Button>().FirstOrDefault();
        if (button?.DataContext is not IconExpressionEditorItem)
        {
            return;
        }

        var index = IconRepeater.GetElementIndex(button);
        var columns = Math.Max(1, (int)(IconRepeater.Bounds.Width / 36));
        var target = e.Key switch
        {
            Key.Left => index - 1,
            Key.Right => index + 1,
            Key.Up => index - columns,
            Key.Down => index + columns,
            Key.Home => 0,
            Key.End => _viewModel.FilteredIcons.Count - 1,
            _ => -1
        };

        // 回车 / 空格由 Button 自身处理为点击，这里只接管方向键导航。
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)
        {
            FocusIcon(Math.Clamp(target, 0, _viewModel.FilteredIcons.Count - 1));
            e.Handled = true;
        }
    }

    private void FocusIcon(int index)
    {
        var element = IconRepeater.GetOrCreateElement(index);
        IconRepeater.UpdateLayout();
        element.BringIntoView();
        element.Focus(NavigationMethod.Directional);
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBrowsing)
        {
            return;
        }

        var root = TopLevel.GetTopLevel(this);
        if (root == null)
        {
            _viewModel.Message = "暂时无法打开文件选择器，请直接输入图片路径。";
            return;
        }

        _viewModel.IsBrowsing = true;
        _viewModel.Message = "";
        EditorFlyout.Hide();
        try
        {
            var files = await root.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择图片",
                AllowMultiple = false,
                FileTypeFilter = [FilePickerFileTypes.ImageAll]
            });

            var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (!Path.IsPathFullyQualified(path))
            {
                _viewModel.Message = "无法直接引用这张图片。请先将它保存到本地，再输入图片路径。";
                return;
            }

            _viewModel.ImagePath = path;
        }
        catch (Exception)
        {
            _viewModel.Message = "无法打开所选图片，请重新选择，或直接输入图片路径。";
        }
        finally
        {
            _viewModel.IsBrowsing = false;
            // 文件选择器会关闭 Flyout，回到页面后重新展开，保持编辑上下文。
            if (TopLevel.GetTopLevel(this) == root && IsEffectivelyVisible)
            {
                EditorFlyout.ShowAt(EditorButton);
            }
        }
    }
}
