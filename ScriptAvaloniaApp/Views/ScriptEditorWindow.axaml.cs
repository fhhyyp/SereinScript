using Avalonia.Controls;
using Avalonia.Interactivity;
using ScriptLang.Lexer;
using ScriptLang.Parser;
using ScriptLang.Runtime;
using ScriptAvaloniaApp.Utils;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using ScriptLang;
using System.IO;

namespace ScriptAvaloniaApp.Views
{
    public partial class ScriptEditorWindow : Window
    {
        private UIPreviewWindow? _previewWindow;

        private string _basePath = string.Empty;

        public ScriptEditorWindow()
        {

            _basePath = Path.Combine(Directory.GetCurrentDirectory(), "Script");

            InitializeComponent();
            ScriptEditor.Text = "Script/ui.script";
            //var defaultScriptPath = Path.Combine(_basePath, "ui.script");
            //var text = File.ReadAllText(defaultScriptPath);


            // 设置默认脚本

            // 注入内置函数
        }

        private async Task Build()
        {
            UpdateStatus("Running...");
            ScriptEngine engine = new ScriptEngine("");
            var mainScriptName = ScriptEditor.Text ?? string.Empty;
            BindingManager.Clear();
            var uiValue =  await engine.LoadAndRunAsync(mainScriptName);
            // 4. 构建 UI
            var uiControl = ScriptUIBuilderV3.BuildUI(uiValue, engine);

            // 5. 显示在预览窗口
            ShowPreviewWindow(uiControl);

            UpdateStatus("Success!");
        }

        private async void RunButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                await Build();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                Debug.WriteLine($"Error: {ex}");
            }
        }

        private void ClearButton_Click(object? sender, RoutedEventArgs e)
        {
            ScriptEditor.Text = string.Empty;
            _previewWindow?.Close();
            _previewWindow = null;
            BindingManager.Clear();
            UpdateStatus("Cleared");
        }

        private void ShowPreviewWindow(Control uiControl)
        {
            if (_previewWindow == null)
            {
                _previewWindow = new UIPreviewWindow();
                _previewWindow.Closed += _previewWindow_Closed;
            }

            _previewWindow.SetContent(uiControl);

            if (_previewWindow.IsVisible)
            {
                _previewWindow.Activate();
            }
            else
            {
                _previewWindow.Show();
            }
        }

        private void _previewWindow_Closed(object? sender, EventArgs e)
        {
            _previewWindow = null;
        }

        private void UpdateStatus(string message)
        {
            StatusLabel.Text = message;
        }
    }
}
