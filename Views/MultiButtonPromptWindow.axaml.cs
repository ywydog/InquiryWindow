using Avalonia.Controls;
using ClassIsland.Core;
using ClassIsland.Core.Controls;
using InquiryWindow.ViewModels;

namespace InquiryWindow.Views;

public partial class MultiButtonPromptWindow : MyWindow
{
    private MultiButtonPromptViewModel? _subscribedVm;

    public MultiButtonPromptWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
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
        Close();
    }

    /// <summary>
    /// 弹窗显示，兼容 root window 不存在的情况。返回 Task 等弹窗关闭。
    /// </summary>
    public async Task ShowDialogCompat()
    {
        var owner = AppBase.Current.GetRootWindow();
        if (owner != null)
        {
            await ShowDialog<object?>(owner);
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

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
