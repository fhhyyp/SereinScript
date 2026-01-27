using Avalonia.Controls;
using ScriptAvaloniaApp.Utils;
using ScriptLang;
using ScriptLang.Lexer;
using ScriptLang.Parser;
using ScriptLang.Runtime;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ScriptAvaloniaApp.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();


            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                string script = """
                let counter = { 
                	value = 0
                }

                let ui = {
                	StackPanel =  { 
                		"TextBox" = {
                            Text = () => counter.value,
                            OnInput = (v) => counter.value = int(v)
                        },
                		TextBlock = {
                			Text = () => "Count: " + counter.value
                		},
                		Button_add = {
                			Content = "自增",
                			OnClick = () => counter.value = counter.value + 1  
                		},
                		Button_clear = {
                			Content = "清空",
                			OnClick = () => counter.value = 0
                		}
                	}
                }
                """;

                ScriptEngine engine = new ScriptEngine();

                var uiValue = await engine.RunAsync(script);

                // 加载 ScriptLang UI 配置
                //var uiValue = (await interpreter.EvaluateAsync(ast, scope)).Value;

                var root = ScriptUIBuilderV3.BuildUI(uiValue, engine);

                this.Content = root;
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }


}