using Avalonia.Controls;
using Avalonia.Interactivity;
using InquiryWindow.Actions;

namespace InquiryWindow.Views;

/// <summary>
/// 「询问窗」弹窗内容（桌面版「交互融合」覆盖窗口专用）。
/// 从 <see cref="InquiryWindowWindow"/> 抽取，供 <see cref="InquiryWindowFusionOverlay"/> 承载。
/// 用户点按钮时通过 <see cref="ResultChosen"/> 通知宿主关闭并返回结果。
/// </summary>
public partial class InquiryWindowDialogContent : UserControl
{
    /// <summary>
    /// 用户做出选择时触发。宿主（交互融合覆盖窗口）订阅它来关闭弹窗并返回结果。
    /// </summary>
    public event Action<InquiryWindowResult>? ResultChosen;

    public InquiryWindowDialogContent()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        ResultChosen?.Invoke(InquiryWindowResult.Cancel);
    }

    private void OnExecuteClick(object? sender, RoutedEventArgs e)
    {
        ResultChosen?.Invoke(InquiryWindowResult.Execute);
    }

    private void OnAcknowledgeClick(object? sender, RoutedEventArgs e)
    {
        ResultChosen?.Invoke(InquiryWindowResult.Acknowledged);
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}