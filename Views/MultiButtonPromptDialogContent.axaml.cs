using Avalonia.Controls;

namespace InquiryWindow.Views;

/// <summary>
/// 「多按钮询问」弹窗内容（桌面版「交互融合」覆盖窗口专用）。
/// 从 <see cref="MultiButtonPromptWindow"/> 抽取，供 <see cref="InquiryWindowFusionOverlay"/> 承载。
/// DataContext 由宿主设为 <see cref="InquiryWindow.ViewModels.MultiButtonPromptViewModel"/>。
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