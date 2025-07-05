using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace UMM
{
    /// <summary>
    /// 选择窗口.xaml 的交互逻辑
    /// </summary>
    public partial class 选择窗口 : Window
    {
        public enum 选择类型 { None, 压缩包, 文件夹 }
        public 选择类型 结果 { get; private set; } = 选择类型.None;

        public 选择窗口()
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        private void 选择压缩包_Click(object sender, RoutedEventArgs e)
        {
            结果 = 选择类型.压缩包;
            this.DialogResult = true;
            this.Close();
        }

        private void 选择文件夹_Click(object sender, RoutedEventArgs e)
        {
            结果 = 选择类型.文件夹;
            this.DialogResult = true;
            this.Close();
        }
    }
}
