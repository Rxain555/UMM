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

namespace UMM
{
    /// <summary>
    /// 命名窗口.xaml 的交互逻辑
    /// </summary>
    public partial class 命名窗口 : Window
    {
        public string 命名结果 { get; private set; }

        public 命名窗口()
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        private void 确认按钮_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(命名输入.Text))
            {
                提示窗口.Show("请输入名称", this);
                return;
            }
            命名结果 = 命名输入.Text.Trim();
            this.DialogResult = true;
            this.Close();
        }
    }
}
