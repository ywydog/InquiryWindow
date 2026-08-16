using Avalonia.Controls;

namespace InquiryWindow.Views;

/// <summary>
/// 「多按钮询问」弹窗内容（Android 分支专用）。
/// 从 <see cref="MultiButtonPromptWindow"/> 抽取，供 FAContentDialog 承载。
/// DataContext 由宿主（<see cref="Services.AndroidDialogService"/>）设为
/// <see cref="InquiryWindow.ViewModels.MultiButtonPromptViewModel"/>。
/// </summary>
public partial class MultiButtonPromptDialogContent : UserControl
{
    public MultiButtonPromptDialogContent()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}