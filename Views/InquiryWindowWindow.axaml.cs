using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using ClassIsland.Core.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using InquiryWindow.Actions;

namespace InquiryWindow.Views;

public partial class InquiryWindowWindow : MyWindow
{
    private readonly ViewModel _vm = new();
    private bool _allowClose;

    public InquiryWindowWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // 拦截关闭：必须通过按钮才能关闭
        Closing += (_, e) =>
        {
            if (!_allowClose)
            {
                e.Cancel = true;
            }
        };

        // 拦截最小化：自动还原
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

    public string WindowTitle
    {
        get => Title ?? "";
        set => Title = value;
    }

    public string DialogTitleSmall
    {
        get => _vm.DialogTitleSmall;
        set => _vm.DialogTitleSmall = value;
    }

    public string DialogTitle
    {
        get => _vm.DialogTitle;
        set => _vm.DialogTitle = value;
    }

    public string DialogBody
    {
        get => _vm.DialogBody;
        set => _vm.DialogBody = value;
    }

    public string PathText
    {
        get => _vm.PathText;
        set => _vm.PathText = value;
    }

    public bool IsPathVisible
    {
        get => _vm.IsPathVisible;
        set => _vm.IsPathVisible = value;
    }

    public Bitmap? Icon
    {
        get => _vm.Icon;
        set
        {
            _vm.Icon = value;
            _vm.IsIconVisible = value != null;
        }
    }

    public bool IsIconVisible
    {
        get => _vm.IsIconVisible;
        set => _vm.IsIconVisible = value;
    }

    public bool CanExecute
    {
        get => _vm.CanExecute;
        set => _vm.CanExecute = value;
    }

    public InquiryWindowResult Result { get; private set; } = InquiryWindowResult.Cancel;

    /// <summary>
    /// 显示确认弹窗并等待用户选择。
    /// 重写父类方法是为了统一通过 <see cref="Result"/> 返回结果，
    /// 并在关闭前用 <see cref="_allowClose"/> 拦截非按钮关闭。
    /// </summary>
    public new async Task<InquiryWindowResult> ShowDialog(Window? owner = null)
    {
        _allowClose = false;

        if (owner != null)
        {
            return await base.ShowDialog<InquiryWindowResult>(owner);
        }

        Show();
        var tcs = new TaskCompletionSource<InquiryWindowResult>();
        Closed += (_, _) => tcs.TrySetResult(Result);
        return await tcs.Task;
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Result = InquiryWindowResult.Cancel;
        CloseProgrammatically();
    }

    private void OnExecuteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Result = InquiryWindowResult.Execute;
        CloseProgrammatically();
    }

    private void CloseProgrammatically()
    {
        _allowClose = true;
        base.Close(Result);
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    public partial class ViewModel : ObservableObject
    {
        [ObservableProperty] private string _dialogTitleSmall = "";
        [ObservableProperty] private string _dialogTitle = "";
        [ObservableProperty] private string _dialogBody = "";
        [ObservableProperty] private string _pathText = "";
        [ObservableProperty] private bool _isPathVisible;
        [ObservableProperty] private Bitmap? _icon;
        [ObservableProperty] private bool _isIconVisible;
        [ObservableProperty] private bool _canExecute = true;
    }
}
