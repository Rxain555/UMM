using System;
using System.Windows;
using System.Windows.Input;
using System.Media;

namespace UMM
{
    /// <summary>
    /// 提示窗口.xaml 的交互逻辑
    /// </summary>
    public partial class 提示窗口 : Window
    {
        public 提示窗口(string message)
        {
            InitializeComponent();
            提示信息.Text = message;
            this.Loaded += (s, e) => AutoResize();
            // 只绑定一次Click事件
            确定按钮.Click += 确定按钮_Click;
        }

        private void AutoResize()
        {
            提示信息.Measure(new Size(MaxWidth, MaxHeight));
            double desiredWidth = Math.Max(MinWidth, Math.Min(提示信息.DesiredSize.Width + 60, MaxWidth));
            double desiredHeight = Math.Max(MinHeight, Math.Min(提示信息.DesiredSize.Height + 100, MaxHeight));
            this.Width = desiredWidth;
            this.Height = desiredHeight;
        }

        public static bool ShowConfirm(string message, Window owner = null)
        {
            SystemSounds.Exclamation.Play();
            var win = new 提示窗口(message);
            if (owner != null) win.Owner = owner;
            win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            return win.ShowDialog() == true;
        }

        public static void Show(string message, Window owner = null)
        {
            SystemSounds.Exclamation.Play(); // 播放Windows默认提示音
            var win = new 提示窗口(message);
            if (owner != null) win.Owner = owner;
            win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            win.ShowDialog();
        }

        private void 确定按钮_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.DialogResult = true;
            }
            catch (InvalidOperationException)
            {
                // 非对话框模式下不能设置DialogResult，只关闭窗口
            }
            this.Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 允许关闭窗口时不设置DialogResult，防止异常
            if (!this.DialogResult.HasValue)
                this.DialogResult = false;
            base.OnClosing(e);
        }
    }
}
