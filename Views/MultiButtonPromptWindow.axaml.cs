using Avalonia.Controls;
using ClassIsland.Core;
using ClassIsland.Core.Controls;
using InquiryWindow.ViewModels;

namespace InquiryWindow.Views;

public partial class MultiButtonPromptWindow : MyWindow
{
    public MultiButtonPromptWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MultiButtonPromptViewModel vm)
        {
            vm.RequestClose -= CloseWindow;
            vm.RequestClose += CloseWindow;
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
            Show();
            var tcs = new TaskCompletionSource<object?>();
            Closed += (_, _) => tcs.TrySetResult(null);
            await tcs.Task;
        }
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
