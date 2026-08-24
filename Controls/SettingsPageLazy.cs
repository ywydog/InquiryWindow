using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;

namespace InquiryWindow.Controls;

/// <summary>
/// 设置页懒加载容器：页面打开时先显示加载指示器，内容延迟到 Loaded 后再渲染并淡入，
/// 避免切换设置页时一次性构建大量控件导致卡顿。
/// 实现思路参考 ClassIsland「Lazy」控件与 SystemTools「设置页懒加载」的设计（自实现，不引入参照代码）。
/// </summary>
public class SettingsPageLazy : ContentControl
{
    private ContentPresenter? _contentPresenter;
    private FAProgressRing? _loadingIndicator;
    private bool _isContentChangesPending;

    public SettingsPageLazy()
    {
        PropertyChanged += OnAnyPropertyChanged;
        Loaded += OnLoaded;
    }

    private void OnAnyPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ContentProperty || e.Property == ContentTemplateProperty)
        {
            UpdateContent();
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // 控件在 Content 变化后、尚未 Loaded 时，先挂起，等 Loaded 后再渲染内容。
        if (_isContentChangesPending)
        {
            UpdateContent();
        }
    }

    private void UpdateContent()
    {
        _isContentChangesPending = true;
        if (!IsLoaded)
        {
            return;
        }

        _isContentChangesPending = false;

        // 先让加载圈渲染一帧，再把真实内容填充进 ContentPresenter，
        // 避免大块内容（如预设列表/详情）一次性构建阻塞页面切换。
        SetLoading(true);
        if (_contentPresenter is not null)
        {
            _contentPresenter.Opacity = 0;
        }

        Dispatcher.Post(() =>
        {
            if (_contentPresenter is not null)
            {
                _contentPresenter.Content = Content;
                _contentPresenter.ContentTemplate = ContentTemplate;
            }
            // 内容布局完成后淡入并收起加载圈。
            Dispatcher.Post(() =>
            {
                SetLoading(false);
                if (_contentPresenter is not null)
                {
                    _contentPresenter.Opacity = 1;
                }
            }, DispatcherPriority.Loaded);
        });
    }

    private void SetLoading(bool show)
    {
        if (_loadingIndicator is null) return;
        _loadingIndicator.IsActive = show;
        _loadingIndicator.IsVisible = show;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        _contentPresenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
        _loadingIndicator = e.NameScope.Find<FAProgressRing>("PART_LoadingIndicator");
        base.OnApplyTemplate(e);
    }
}
