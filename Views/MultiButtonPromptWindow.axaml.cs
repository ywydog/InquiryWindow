using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ClassIsland.Core;
using ClassIsland.Core.Controls;
using InquiryWindow.ViewModels;

namespace InquiryWindow.Views;

public partial class MultiButtonPromptWindow : MyWindow
{
    private MultiButtonPromptViewModel? _subscribedVm;
    private bool _allowClose;

    public MultiButtonPromptWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // 拦截关闭：默认必须通过按钮才能关闭（与 v1 InquiryWindowWindow 一致）。
        // 「详细设置 → 窗口关闭」开启时，标题栏 X 也允许关闭，并按「关闭时执行的按钮」处理。
        Closing += (_, e) =>
        {
            if (_allowClose)
            {
                return;
            }

            e.Cancel = true;
            if (DataContext is MultiButtonPromptViewModel { IsClosable: true } vm)
            {
                // 在 Closing 内同步 Close() 会重入，交给 UI 队列等本次关闭请求结束再处理。
                Dispatcher.UIThread.Post(() => { _ = vm.CloseByUserAsync(); });
            }
        };

        // 拦截最小化：自动还原（与 v1 InquiryWindowWindow 一致）
        this.GetObservable(WindowStateProperty).Subscribe(s =>
        {
            if (s == WindowState.Minimized)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    WindowState = WindowState.Normal;
                    Activate();
                }, DispatcherPriority.MaxValue);
            }
        });
    }

    /// <summary>
    /// OS 窗口标题栏文字。包一层是因为 <see cref="MyWindow"/> 的 Title 是自定义 DP，
    /// 直接绑 {Binding Title} 不一定会推送；v1 也是这种写法。
    /// </summary>
    public string WindowTitle
    {
        get => Title ?? "";
        set => Title = value;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        // 先拆旧 VM 的订阅，避免泄漏。
        if (_subscribedVm is not null)
        {
            _subscribedVm.RequestClose -= CloseWindow;
            _subscribedVm.Cleanup();
            _subscribedVm = null;
        }
        if (DataContext is MultiButtonPromptViewModel vm)
        {
            vm.RequestClose += CloseWindow;
            _subscribedVm = vm;
        }
    }

    private void CloseWindow()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_subscribedVm is not null)
        {
            _subscribedVm.RequestClose -= CloseWindow;
            _subscribedVm.Cleanup();
            _subscribedVm = null;
        }
        base.OnClosed(e);
    }

    /// <summary>
    /// 弹窗显示，兼容 root window 不存在的情况。返回 Task 等弹窗关闭。
    /// </summary>
    public async Task ShowDialogCompat()
    {
        var owner = AppBase.Current.GetRootWindow();
        if (owner is Window windowOwner)
        {
            await ShowDialog<object?>(windowOwner);
        }
        else
        {
            // 先订阅 Closed，再 Show()：避免窗口在订阅前被同步关闭导致 Task 永远不 complete。
            var tcs = new TaskCompletionSource<object?>();
            void OnClosed(object? s, System.EventArgs e)
            {
                Closed -= OnClosed;
                tcs.TrySetResult(null);
            }
            Closed += OnClosed;
            Show();
            await tcs.Task;
        }
    }
}
