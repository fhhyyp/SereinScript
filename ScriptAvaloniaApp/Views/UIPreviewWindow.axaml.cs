using Avalonia.Controls;

namespace ScriptAvaloniaApp.Views
{
    public partial class UIPreviewWindow : Window
    {
        public UIPreviewWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 设置窗口内容
        /// </summary>
        public void SetContent(Avalonia.Controls.Control content)
        {
            ContentContainer.Content = content;
        }
    }
}
