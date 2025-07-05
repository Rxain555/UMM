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
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using System.IO;

namespace UMM
{
    /// <summary>
    /// 添加游戏窗口.xaml 的交互逻辑
    /// </summary>
    public partial class 添加游戏窗口 : Window
    {
        public string 游戏名称结果 { get; private set; }
        public string 游戏路径结果 { get; private set; }
        public string 模组路径结果 { get; private set; }

        public 添加游戏窗口()
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        private void 选择游戏路径_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "请选择游戏程序";
            dialog.Filter = "可执行文件 (*.exe)|*.exe";
            dialog.CheckFileExists = true;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                游戏路径.Text = dialog.FileName;
            }
        }

        private void 选择模组路径_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            dialog.Description = "请选择模组文件夹";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                模组路径.Text = dialog.SelectedPath;
            }
        }

        private void 确认_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(游戏名称.Text))
            {
                提示窗口.Show("请输入游戏名称", this);
                return;
            }
            if (string.IsNullOrWhiteSpace(游戏路径.Text))
            {
                提示窗口.Show("请选择游戏路径", this);
                return;
            }
            if (string.IsNullOrWhiteSpace(模组路径.Text))
            {
                提示窗口.Show("请选择模组路径", this);
                return;
            }
            游戏名称结果 = 游戏名称.Text.Trim();
            游戏路径结果 = 游戏路径.Text.Trim();
            模组路径结果 = 模组路径.Text.Trim();
            this.DialogResult = true;
            this.Close();
        }
    }
}
