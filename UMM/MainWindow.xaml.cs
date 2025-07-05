using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using SharpCompress.Archives;
using SharpCompress.Common;
using System.Drawing;
using System.ComponentModel;

namespace UMM
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private string 备份路径缓存 = null;
        private string 配置文件夹 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UMMData");
        private string 配置文件 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UMMData", "UMMData.json");
        private JObject 当前数据 = new JObject();
        private ObservableCollection<string> 游戏列表数据 = new ObservableCollection<string>();
        private ObservableCollection<string> 模组分类数据 = new ObservableCollection<string>();
        private ObservableCollection<string> 模组列表数据 = new ObservableCollection<string>();
        private ObservableCollection<ModItem> 模组列表数据Ex = new ObservableCollection<ModItem>();

        private ModItem _selectedModItem;
        public ModItem SelectedModItem
        {
            get => _selectedModItem;
            set
            {
                _selectedModItem = value;
                UpdateModDetailEnableState();
            }
        }

        // 右键菜单相关字段
        private object _rightClickTarget;

        private bool isModListRefreshing = false;

        public MainWindow()
        {
            InitializeComponent();
            // 恢复窗口大小和位置
            if (File.Exists(配置文件))
            {
                try
                {
                    var json = JObject.Parse(File.ReadAllText(配置文件));
                    if (json["windowLeft"] != null && json["windowTop"] != null && json["windowWidth"] != null && json["windowHeight"] != null)
                    {
                        this.Left = (double)json["windowLeft"];
                        this.Top = (double)json["windowTop"];
                        this.Width = (double)json["windowWidth"];
                        this.Height = (double)json["windowHeight"];
                        this.WindowStartupLocation = WindowStartupLocation.Manual;
                    }
                }
                catch { }
            }
            添加备份路径.Click += 添加备份路径_Click;
            添加游戏.Click += 添加游戏_Click;
            添加模组分类.Click += 添加模组分类_Click;
            添加模组.Click += 添加模组_Click;
            添加图片.Click += 添加图片_Click;
            搜索按钮.Click += 搜索按钮_Click;
            RegisterGlobalClickInterceptor(this);
            游戏列表.ItemsSource = 游戏列表数据;
            模组分类.ItemsSource = 模组分类数据;
            模组列表.ItemsSource = 模组列表数据Ex;
            游戏列表.SelectionChanged += 游戏列表_SelectionChanged;
            游戏列表.SelectionChanged += (s, e) =>
            {
                添加模组分类.IsEnabled = 游戏列表.SelectedItem != null;
            };
            模组分类.SelectionChanged += 模组分类_SelectionChanged;
            模组列表.SelectionChanged += 模组列表_SelectionChanged;
            模组列表.AddHandler(CheckBox.ClickEvent, new RoutedEventHandler(ModCheckBox_Click));
            游戏列表.MouseRightButtonUp += 游戏列表_MouseRightButtonUp;
            模组分类.MouseRightButtonUp += 模组分类_MouseRightButtonUp;
            模组列表.MouseRightButtonUp += 模组列表_MouseRightButtonUp;
            加载配置并刷新界面();
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            当前数据["windowLeft"] = this.Left;
            当前数据["windowTop"] = this.Top;
            当前数据["windowWidth"] = this.Width;
            当前数据["windowHeight"] = this.Height;
            保存配置();
        }

        private void ModCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (isModListRefreshing) return;
            if (e.OriginalSource is CheckBox cb && cb.DataContext is ModItem mod)
            {
                // 使点击复选框时该项也被选中
                模组列表.SelectedItem = mod;
                if (cb.IsChecked == true)
                {
                    EnableMod(mod);
                }
                else
                {
                    DisableMod(mod);
                }
                UpdateModDetailEnableState();
            }
        }

        private void EnableMod(ModItem mod)
        {
            if (游戏列表.SelectedItem is string game && 模组分类.SelectedItem is string cat && !string.IsNullOrWhiteSpace(模组路径.Text))
            {
                string modDir = System.IO.Path.Combine(备份路径缓存, "游戏", game, cat, mod.Name);
                string modPath = 模组路径.Text;
                if (!Directory.Exists(modDir) || !Directory.Exists(modPath)) return;
                var subDirs = Directory.GetDirectories(modDir);
                mod.CopiedFolderNames.Clear();
                foreach (var sub in subDirs)
                {
                    string folderName = System.IO.Path.GetFileName(sub);
                    string dest = System.IO.Path.Combine(modPath, folderName);
                    string finalDest = dest;
                    int idx = 1;
                    while (Directory.Exists(finalDest))
                    {
                        finalDest = dest + "_" + idx;
                        idx++;
                    }
                    CopyDirectory(sub, finalDest);
                    mod.CopiedFolderNames.Add(System.IO.Path.GetFileName(finalDest));
                }
                mod.IsEnabled = true;
                // 记录到配置文件
                if (当前数据["modStates"] == null) 当前数据["modStates"] = new JObject();
                var modsState = (JObject)当前数据["modStates"];
                modsState[game + "|" + cat + "|" + mod.Name] = new JArray(mod.CopiedFolderNames);
                保存配置();
                刷新已启用模组统计();
            }
        }

        private void DisableMod(ModItem mod)
        {
            if (string.IsNullOrWhiteSpace(模组路径.Text)) return;
            string modPath = 模组路径.Text;
            // 先备份要删除的文件夹名
            var foldersToDelete = mod.CopiedFolderNames.ToList();
            mod.CopiedFolderNames.Clear();
            mod.IsEnabled = false;
            // 移除配置文件记录
            if (当前数据["modStates"] is JObject modsState)
            {
                string key = (游戏列表.SelectedItem?.ToString() ?? "") + "|" + (模组分类.SelectedItem?.ToString() ?? "") + "|" + mod.Name;
                modsState.Remove(key);
                保存配置();
                刷新已启用模组统计();
            }
            // 删除模组路径下的文件夹（必须在清空CopiedFolderNames后再删，否则下次启用会出错）
            foreach (var folder in foldersToDelete)
            {
                string toDelete = System.IO.Path.Combine(modPath, folder);
                try
                {
                    if (Directory.Exists(toDelete))
                        Directory.Delete(toDelete, true);
                }
                catch (Exception ex)
                {
                    提示窗口.Show($"删除模组文件夹失败：{folder}\n{ex.Message}", this);
                }
            }
        }

        private void 加载配置并刷新界面()
        {
            if (File.Exists(配置文件))
            {
                当前数据 = JObject.Parse(File.ReadAllText(配置文件));
                if (当前数据["backupPath"] != null)
                {
                    备份路径缓存 = 当前数据["backupPath"].ToString();
                    备份路径.Text = 备份路径缓存;
                    刷新游戏列表();
                    // 记忆选中项并联动父项
                    string game = 当前数据["selectedGame"]?.ToString();
                    string cat = 当前数据["selectedCategory"]?.ToString();
                    string mod = 当前数据["selectedMod"]?.ToString();
                    if (!string.IsNullOrEmpty(game) && 游戏列表数据.Contains(game))
                    {
                        游戏列表.SelectedItem = game;
                        刷新模组分类(game);
                        if (!string.IsNullOrEmpty(cat) && 模组分类数据.Contains(cat))
                        {
                            模组分类.SelectedItem = cat;
                            刷新模组列表(game, cat);
                            if (!string.IsNullOrEmpty(mod) && 模组列表数据.Contains(mod))
                            {
                                模组列表.SelectedItem = mod;
                            }
                            else
                            {
                                模组列表.SelectedItem = null;
                            }
                        }
                        else
                        {
                            模组分类.SelectedItem = null;
                            模组列表数据.Clear();
                            模组列表.SelectedItem = null;
                        }
                    }
                    else
                    {
                        游戏列表.SelectedItem = null;
                        模组分类数据.Clear();
                        模组分类.SelectedItem = null;
                        模组列表数据.Clear();
                        模组列表.SelectedItem = null;
                    }
                }
            }
        }

        private void 保存配置()
        {
            if (!Directory.Exists(配置文件夹))
                Directory.CreateDirectory(配置文件夹);
            当前数据["backupPath"] = 备份路径缓存;
            File.WriteAllText(配置文件, 当前数据.ToString());
        }

        private void 添加备份路径_Click(object sender, RoutedEventArgs e)
        {
            string 旧备份路径 = 备份路径缓存;
            using (var dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = "请选择备份路径";
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    string 新备份路径 = dialog.SelectedPath;
                    // 递归复制旧备份路径下所有文件夹和文件到新备份路径
                    if (!string.IsNullOrWhiteSpace(旧备份路径) && Directory.Exists(旧备份路径))
                    {
                        foreach (var dir in Directory.GetDirectories(旧备份路径))
                        {
                            string dirName = System.IO.Path.GetFileName(dir);
                            string destDir = System.IO.Path.Combine(新备份路径, dirName);
                            CopyDirectory(dir, destDir);
                        }
                        foreach (var file in Directory.GetFiles(旧备份路径))
                        {
                            string destFile = System.IO.Path.Combine(新备份路径, System.IO.Path.GetFileName(file));
                            File.Copy(file, destFile, true);
                        }
                    }
                    备份路径缓存 = 新备份路径;
                    当前数据["backupPath"] = 备份路径缓存;
                    备份路径.Text = 备份路径缓存; // 立即刷新界面显示
                    保存配置();
                    // 新建“游戏”文件夹
                    var 游戏文件夹 = System.IO.Path.Combine(备份路径缓存, "游戏");
                    if (!Directory.Exists(游戏文件夹))
                        Directory.CreateDirectory(游戏文件夹);
                    // 清空并刷新所有列表
                    游戏列表数据.Clear();
                    模组分类数据.Clear();
                    模组列表数据.Clear();
                    模组列表数据Ex.Clear();
                    游戏列表.SelectedItem = null;
                    模组分类.SelectedItem = null;
                    模组列表.SelectedItem = null;
                    刷新游戏列表();
                    // 清空路径显示
                    游戏路径.Text = "";
                    模组路径.Text = "";
                }
            }
        }

        private void 添加游戏_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(备份路径缓存))
            {
                提示窗口.Show("请先添加备份路径", this);
                return;
            }
            var win = new 添加游戏窗口();
            win.Owner = this;
            if (win.ShowDialog() == true)
            {
                // 1. 读取AppData配置文件
                JObject data = 当前数据;
                if (data["games"] == null)
                    data["games"] = new JArray();
                var games = (JArray)data["games"];
                // 2. 检查是否重名
                foreach (var g in games)
                {
                    if (g["name"] != null && g["name"].ToString() == win.游戏名称结果)
                    {
                        提示窗口.Show("该游戏名称已存在！", this);
                        return;
                    }
                }
                // 3. 添加新游戏
                var newGame = new JObject
                {
                    ["name"] = win.游戏名称结果,
                    ["gamePath"] = win.游戏路径结果,
                    ["modPath"] = win.模组路径结果
                };
                games.Add(newGame);
                当前数据["games"] = games;
                保存配置();
                // 4. 在备份路径/游戏 下新建文件夹
                var gameDir = System.IO.Path.Combine(备份路径缓存, "游戏", win.游戏名称结果);
                if (!System.IO.Directory.Exists(gameDir))
                    System.IO.Directory.CreateDirectory(gameDir);
                // 5. 刷新游戏列表并选中新游戏
                刷新游戏列表();
                游戏列表.SelectedItem = win.游戏名称结果;
            }
        }

        private void 添加模组分类_Click(object sender, RoutedEventArgs e)
        {
            if (游戏列表.SelectedItem is string gameName)
            {
                var win = new 命名窗口();
                win.Owner = this;
                if (win.ShowDialog() == true)
                {
                    // 新建文件夹
                    var catDir = System.IO.Path.Combine(备份路径缓存, "游戏", gameName, win.命名结果);
                    if (!System.IO.Directory.Exists(catDir))
                    {
                        System.IO.Directory.CreateDirectory(catDir);
                        // 刷新模组分类并选中新建项
                        刷新模组分类(gameName);
                        模组分类.SelectedItem = win.命名结果;
                    }
                    else
                    {
                        提示窗口.Show("该分类已存在！", this);
                    }
                }
            }
        }

        // 修正添加模组后自动选中项，类型为ModItem
        private void 添加模组_Click(object sender, RoutedEventArgs e)
        {
            if (!(游戏列表.SelectedItem is string gameName && 模组分类.SelectedItem is string catName))
                return;
            var selectWin = new 选择窗口 { Owner = this };
            if (selectWin.ShowDialog() != true) return;
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "UMM_ImportTemp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string importRoot = null;
            string archivePath = null;
            string userSelectName = null;
            var tempUnpackDirs = new List<string>();
            if (selectWin.结果 == 选择窗口.选择类型.压缩包)
            {
                var dialog = new Forms.OpenFileDialog();
                dialog.Title = "请选择压缩包";
                dialog.Filter = "压缩包文件 (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z|所有文件|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
                archivePath = dialog.FileName;
                userSelectName = System.IO.Path.GetFileNameWithoutExtension(archivePath);
                string ext = System.IO.Path.GetExtension(archivePath).ToLower();
                if (ext == ".zip" || ext == ".rar" || ext == ".7z")
                {
                    using (var archive = ArchiveFactory.Open(archivePath))
                    {
                        string unpackDirName = System.IO.Path.GetFileNameWithoutExtension(archivePath);
                        string tempUnpack = System.IO.Path.Combine(tempDir, unpackDirName);
                        Directory.CreateDirectory(tempUnpack);
                        foreach (var entry in archive.Entries)
                        {
                            if (!entry.IsDirectory)
                            {
                                string destPath = System.IO.Path.Combine(tempUnpack, entry.Key);
                                string destDir = System.IO.Path.GetDirectoryName(destPath);
                                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                                entry.WriteToFile(destPath, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                            }
                        }
                        importRoot = tempUnpack;
                        tempUnpackDirs.Add(tempUnpack);
                    }
                }
                else
                {
                    提示窗口.Show("暂不支持此压缩格式，请手动解压后导入。", this);
                    Directory.Delete(tempDir, true);
                    return;
                }
            }
            else if (selectWin.结果 == 选择窗口.选择类型.文件夹)
            {
                var dialog = new Forms.FolderBrowserDialog();
                dialog.Description = "请选择模组文件夹";
                if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
                importRoot = dialog.SelectedPath;
                userSelectName = new DirectoryInfo(importRoot).Name;
            }
            else
            {
                return;
            }
            // 1. 寻找所有标准模组文件夹（含标准模组文件的文件夹）
            var modFolders = FindAllStandardModFolders(importRoot, tempUnpackDirs);
            if (modFolders.Count == 0)
            {
                提示窗口.Show("未检测到标准模组文件（.pak等）！", this);
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                return;
            }
            var catDir = System.IO.Path.Combine(备份路径缓存, "游戏", gameName, catName);
            string firstModName = null;
            if (modFolders.Count == 1)
            {
                string modName = userSelectName;
                string destModDir = System.IO.Path.Combine(catDir, modName);
                if (Directory.Exists(destModDir))
                    destModDir += "_" + DateTime.Now.ToString("MMddHHmmss");
                string destInner = System.IO.Path.Combine(destModDir, modName);
                Directory.CreateDirectory(destInner);
                foreach (var file in Directory.GetFiles(modFolders[0]))
                {
                    if (IsModFile(file))
                        File.Copy(file, System.IO.Path.Combine(destInner, System.IO.Path.GetFileName(file)), true);
                }
                firstModName = modName;
            }
            else
            {
                var modRootNames = new HashSet<string>();
                foreach (var folder in modFolders)
                {
                    string modName = FindForkName(folder, importRoot);
                    if (string.IsNullOrWhiteSpace(modName))
                        modName = new DirectoryInfo(folder).Name;
                    string destModDir = System.IO.Path.Combine(catDir, modName);
                    if (Directory.Exists(destModDir) || modRootNames.Contains(modName))
                        destModDir += "_" + DateTime.Now.ToString("MMddHHmmss");
                    string destInner = System.IO.Path.Combine(destModDir, modName);
                    Directory.CreateDirectory(destInner);
                    foreach (var file in Directory.GetFiles(folder))
                    {
                        if (IsModFile(file))
                            File.Copy(file, System.IO.Path.Combine(destInner, System.IO.Path.GetFileName(file)), true);
                    }
                    modRootNames.Add(modName);
                    if (firstModName == null) firstModName = modName;
                }
            }
            刷新模组列表(gameName, catName);
            // 自动选中刚添加的模组项（ModItem类型）
            if (firstModName != null)
            {
                var matchMod = 模组列表数据Ex.FirstOrDefault(m => m.Name == firstModName);
                if (matchMod != null)
                    模组列表.SelectedItem = matchMod;
            }
            foreach (var dir in tempUnpackDirs)
            {
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
            }
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        // 修正递归查找标准模组文件夹，避免重复导入
        private List<string> FindAllStandardModFolders(string root, List<string> tempUnpackDirs)
        {
            var result = new List<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Search(string dir)
            {
                if (!visited.Add(dir)) return; // 防止重复遍历
                // 1. 先查找当前目录下的压缩包，自动解压到以压缩包名为名的子目录并递归
                foreach (var file in Directory.GetFiles(dir))
                {
                    string ext = System.IO.Path.GetExtension(file).ToLower();
                    if (ext == ".zip" || ext == ".rar" || ext == ".7z")
                    {
                        string unpackDirName = System.IO.Path.GetFileNameWithoutExtension(file);
                        string tempUnpack = System.IO.Path.Combine(dir, unpackDirName);
                        if (!Directory.Exists(tempUnpack))
                            Directory.CreateDirectory(tempUnpack);
                        using (var archive = ArchiveFactory.Open(file))
                        {
                            foreach (var entry in archive.Entries)
                            {
                                if (!entry.IsDirectory)
                                {
                                    string destPath = System.IO.Path.Combine(tempUnpack, entry.Key);
                                    string destDir = System.IO.Path.GetDirectoryName(destPath);
                                    if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                                    entry.WriteToFile(destPath, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                                }
                            }
                        }
                        tempUnpackDirs.Add(tempUnpack);
                        Search(tempUnpack);
                    }
                }
                // 2. 查找标准模组文件夹（只在当前目录下查找一次）
                var files = Directory.GetFiles(dir);
                if (files.Any(f => IsModFile(f)))
                    result.Add(dir);
                // 3. 递归其它子文件夹（不递归已解压的压缩包目录）
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    // 如果是刚刚解压出来的目录，已经递归过了，不再递归
                    if (visited.Contains(sub)) continue;
                    Search(sub);
                }
            }
            Search(root);
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        // 分叉点命名
        private string FindForkName(string folder, string importRoot)
        {
            var dir = new DirectoryInfo(folder);
            while (dir.Parent != null && dir.Parent.FullName != importRoot)
            {
                var siblings = dir.Parent.GetDirectories();
                if (siblings.Length > 1) return dir.Name;
                dir = dir.Parent;
            }
            return dir.Name;
        }

        private bool IsModFile(string file)
        {
            string ext = System.IO.Path.GetExtension(file).ToLower();
            return ext == ".pak" || ext == ".ucas" || ext == ".utoc" || ext == ".dll" || ext == ".lua" || ext == ".ini";
        }

        private void 刷新游戏列表()
        {
            游戏列表数据.Clear();
            if (!string.IsNullOrWhiteSpace(备份路径缓存))
            {
                var gameRoot = System.IO.Path.Combine(备份路径缓存, "游戏");
                if (Directory.Exists(gameRoot))
                {
                    foreach (var dir in Directory.GetDirectories(gameRoot))
                    {
                        游戏列表数据.Add(System.IO.Path.GetFileName(dir));
                    }
                }
            }
        }

        // 游戏列表选中时，刷新下级并取消下级所有选中
        private void 游戏列表_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (游戏列表.SelectedItem is string game)
            {
                当前数据["selectedGame"] = game;
                // 查找并显示游戏路径和模组路径
                var games = 当前数据["games"] as JArray;
                if (games != null)
                {
                    foreach (var g in games)
                    {
                        if (g["name"] != null && g["name"].ToString() == game)
                        {
                            游戏路径.Text = g["gamePath"]?.ToString() ?? "";
                            模组路径.Text = g["modPath"]?.ToString() ?? "";
                            break;
                        }
                    }
                }
                // 联动父项：刷新模组分类并记忆
                刷新模组分类(game);
                // 只选中分类，不自动选中模组分类和模组列表
                模组分类.SelectedItem = null;
                模组列表数据.Clear();
                模组列表数据Ex.Clear();
                模组列表.SelectedItem = null;
                保存配置();
                刷新已启用模组统计();
            }
            else
            {
                当前数据["selectedGame"] = null;
                当前数据["selectedCategory"] = null;
                当前数据["selectedMod"] = null;
                保存配置();
                游戏路径.Text = "";
                模组路径.Text = "";
                模组分类数据.Clear();
                模组分类.SelectedItem = null;
                模组列表数据.Clear();
                模组列表.SelectedItem = null;
                刷新已启用模组统计();
            }
        }

        private void 刷新模组分类(string game)
        {
            模组分类数据.Clear();
            var catRoot = System.IO.Path.Combine(备份路径缓存, "游戏", game);
            if (Directory.Exists(catRoot))
            {
                foreach (var dir in Directory.GetDirectories(catRoot))
                {
                    模组分类数据.Add(System.IO.Path.GetFileName(dir));
                }
            }
        }

        // 模组分类选中时，刷新下级并取消下级所有选中
        private void 模组分类_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (模组分类.SelectedItem is string cat && 游戏列表.SelectedItem is string game)
            {
                当前数据["selectedCategory"] = cat;
                // 联动父项：刷新模组列表并记忆
                刷新模组列表(game, cat);
                string mod = 当前数据["selectedMod"]?.ToString();
                if (!string.IsNullOrEmpty(mod) && 模组列表数据.Contains(mod))
                    模组列表.SelectedItem = mod;
                else
                    模组列表.SelectedItem = null;
                保存配置();
                刷新已启用模组统计();
            }
            else
            {
                当前数据["selectedCategory"] = null;
                当前数据["selectedMod"] = null;
                保存配置();
                模组列表数据.Clear();
                模组列表.SelectedItem = null;
                刷新已启用模组统计();
            }
        }

        private void 刷新模组列表(string game, string cat)
        {
            isModListRefreshing = true;
            模组列表数据.Clear();
            模组列表数据Ex.Clear();
            var modRoot = System.IO.Path.Combine(备份路径缓存, "游戏", game, cat);
            if (Directory.Exists(modRoot))
            {
                foreach (var dir in Directory.GetDirectories(modRoot))
                {
                    var name = System.IO.Path.GetFileName(dir);
                    模组列表数据.Add(name);
                    var item = new ModItem { Name = name };
                    // 只根据配置文件记录的 CopiedFolderNames 判断是否启用
                    var modsState = 当前数据["modStates"] as JObject;
                    if (modsState != null && modsState.TryGetValue(game + "|" + cat + "|" + name, out var stateToken))
                    {
                        var arr = stateToken as JArray;
                        if (arr != null && arr.Count > 0)
                        {
                            item.IsEnabled = true;
                            item.CopiedFolderNames = arr.Select(t => t.ToString()).ToList();
                        }
                    }
                    模组列表数据Ex.Add(item);
                }
            }
            模组列表.ItemsSource = 模组列表数据Ex;
            isModListRefreshing = false;
        }

        // 模组列表选中时，始终刷新图片和详情
        private void 模组列表_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (模组列表.SelectedItem is ModItem mod && 游戏列表.SelectedItem is string game && 模组分类.SelectedItem is string cat)
            {
                var modDir = System.IO.Path.Combine(备份路径缓存, "游戏", game, cat, mod.Name);
                RefreshModImages(modDir);
                // 显示模组名称
                模组详情模组名称.Text = mod.Name;
                // 显示原始名称（子文件夹名）
                var subDirs = Directory.Exists(modDir) ? Directory.GetDirectories(modDir) : new string[0];
                模组详情模组原始名称.Text = subDirs.Length > 0 ? new DirectoryInfo(subDirs[0]).Name : "";
                // 启用状态
                SelectedModItem = mod;
                UpdateModDetailEnableState();
                刷新已启用模组统计();
            }
            else
            {
                图片显示.Source = null;
                模组详情模组名称.Text = "";
                模组详情模组原始名称.Text = "";
                模组详情图片名称.Text = "";
                ShowNoImage();
                SelectedModItem = null;
                UpdateModDetailEnableState();
                刷新已启用模组统计();
            }
        }

        private void UpdateModDetailEnableState()
        {
            if (SelectedModItem != null)
            {
                if (SelectedModItem.IsEnabled)
                {
                    模组详情是否已启用.Text = "已启用";
                    模组详情是否已启用.Foreground = new SolidColorBrush(Colors.Green);
                }
                else
                {
                    模组详情是否已启用.Text = "未启用";
                    模组详情是否已启用.Foreground = new SolidColorBrush(Colors.Red);
                }
            }
            else
            {
                模组详情是否已启用.Text = "";
            }
        }

        // 递归复制文件夹
        private void CopyDirectory(string sourceDir, string destDir)
        {
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string dirName = System.IO.Path.GetFileName(dir);
                CopyDirectory(dir, System.IO.Path.Combine(destDir, dirName));
            }
        }

        // 修正添加图片按钮事件，支持ModItem类型
        private void 添加图片_Click(object sender, RoutedEventArgs e)
        {
            if (模组列表.SelectedItem is ModItem mod && 游戏列表.SelectedItem is string game && 模组分类.SelectedItem is string cat)
            {
                var dialog = new Forms.OpenFileDialog();
                dialog.Title = "请选择图片";
                dialog.Filter = "图片文件 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
                string src = dialog.FileName;
                string ext = System.IO.Path.GetExtension(src);
                var modDir = System.IO.Path.Combine(备份路径缓存, "游戏", game, cat, mod.Name);
                if (!Directory.Exists(modDir)) Directory.CreateDirectory(modDir);
                // 1. 先清除旧图片（刷新图片控件，避免文件占用）
                var oldImgs = Directory.GetFiles(modDir, "*.png").Concat(Directory.GetFiles(modDir, "*.jpg")).Concat(Directory.GetFiles(modDir, "*.jpeg")).ToArray();
                图片显示.Source = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                foreach (var img in oldImgs)
                {
                    try { File.Delete(img); } catch { }
                }
                // 2. 复制新图片
                string dest = System.IO.Path.Combine(modDir, System.IO.Path.GetFileName(src));
                File.Copy(src, dest, true);
                // 3. 立即刷新显示新图片
                RefreshModImages(modDir);
            }
        }

        // 刷新图片显示（为后续切换功能预留）
        private void RefreshModImages(string modDir)
        {
            var imgFiles = Directory.Exists(modDir)
                ? Directory.GetFiles(modDir, "*.png").Concat(Directory.GetFiles(modDir, "*.jpg")).Concat(Directory.GetFiles(modDir, "*.jpeg")).ToArray()
                : new string[0];
            if (imgFiles.Length > 0)
            {
                try
                {
                    // 用FileStream方式加载图片，避免文件被锁定，确保图片能被覆盖和刷新
                    using (var fs = new FileStream(imgFiles[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = fs;
                        bitmap.EndInit();
                        bitmap.Freeze(); // 避免跨线程问题
                        图片显示.Source = bitmap;
                    }
                    模组详情图片名称.Text = System.IO.Path.GetFileName(imgFiles[0]);
                    if (无图片提示 != null) 无图片提示.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    ShowNoImage("图片加载失败");
                    模组详情图片名称.Text = "";
                }
            }
            else
            {
                ShowNoImage();
                模组详情图片名称.Text = "";
            }
        }

        private void ShowNoImage(string text = "无图片")
        {
            图片显示.Source = null;
            if (无图片提示 != null)
            {
                无图片提示.Text = text;
                无图片提示.Visibility = Visibility.Visible;
            }
        }

        private void RegisterGlobalClickInterceptor(DependencyObject parent)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(parent))
            {
                if (child is System.Windows.Controls.Primitives.ButtonBase btn)
                {
                    if (btn == 添加备份路径 || btn == 添加游戏) continue;
                    if (btn == 添加模组分类)
                    {
                        btn.Click += (s, e) => {
                            if (游戏列表.SelectedItem == null)
                            {
                                e.Handled = true;
                                提示窗口.Show("请先选中一个游戏", this);
                            }
                        };
                    }
                    else if (btn == 添加模组)
                    {
                        btn.Click += (s, e) => {
                            if (模组分类.SelectedItem == null)
                            {
                                e.Handled = true;
                                提示窗口.Show("请先选中一个模组分类", this);
                            }
                        };
                    }
                    else if (btn == 添加图片)
                    {
                        btn.Click += (s, e) => {
                            if (模组列表.SelectedItem == null)
                            {
                                e.Handled = true;
                                提示窗口.Show("请先选中一个模组", this);
                            }
                        };
                    }
                    else
                    {
                        btn.Click += InterceptIfNoBackupPath;
                    }
                }
                else if (child is System.Windows.Controls.TextBox tb)
                {
                    tb.PreviewMouseDown += InterceptIfNoBackupPath;
                }
                else if (child is System.Windows.Controls.ListBox lb)
                {
                    lb.PreviewMouseDown += InterceptIfNoBackupPath;
                }
                else if (child is DependencyObject dep)
                {
                    RegisterGlobalClickInterceptor(dep);
                }
            }
        }

        private void InterceptIfNoBackupPath(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(备份路径缓存))
            {
                e.Handled = true;
                提示窗口.Show("请先添加备份路径", this);
            }
        }

        private void InterceptIfNoBackupPath(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(备份路径缓存))
            {
                e.Handled = true;
                提示窗口.Show("请先添加备份路径", this);
            }
        }

        private void 刷新已启用模组统计()
        {
            if (游戏列表.SelectedItem is string game)
            {
                int total = 0, enabled = 0;
                var catRoot = System.IO.Path.Combine(备份路径缓存, "游戏", game);
                if (Directory.Exists(catRoot))
                {
                    foreach (var cat in Directory.GetDirectories(catRoot).Select(p => System.IO.Path.GetFileName(p)))
                    {
                        var modRoot = System.IO.Path.Combine(catRoot, cat);
                        if (Directory.Exists(modRoot))
                        {
                            foreach (var mod in Directory.GetDirectories(modRoot).Select(p => System.IO.Path.GetFileName(p)))
                            {
                                total++;
                                var modsState = 当前数据["modStates"] as JObject;
                                if (modsState != null && modsState.TryGetValue(game + "|" + cat + "|" + mod, out var stateToken))
                                {
                                    var arr = stateToken as JArray;
                                    if (arr != null && arr.Count > 0) enabled++;
                                }
                            }
                        }
                    }
                }
                已启用模组.Text = $"{enabled}/{total}";
            }
            else
            {
                已启用模组.Text = "";
            }
        }

        private void 搜索按钮_Click(object sender, RoutedEventArgs e)
        {
            string keyword = 搜索输入.Text.Trim();
            if (string.IsNullOrEmpty(keyword)) return;
            // 1. 优先模组列表（需遍历所有游戏和分类）
            foreach (var game in 游戏列表数据)
            {
                var catRoot = System.IO.Path.Combine(备份路径缓存, "游戏", game);
                if (!Directory.Exists(catRoot)) continue;
                foreach (var cat in Directory.GetDirectories(catRoot).Select(p => System.IO.Path.GetFileName(p)))
                {
                    var modRoot = System.IO.Path.Combine(catRoot, cat);
                    if (!Directory.Exists(modRoot)) continue;
                    foreach (var mod in Directory.GetDirectories(modRoot).Select(p => System.IO.Path.GetFileName(p)))
                    {
                        if (mod.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // 选中父项并刷新下级
                            游戏列表.SelectedItem = game;
                            刷新模组分类(game);
                            模组分类.SelectedItem = cat;
                            刷新模组列表(game, cat);
                            var matchMod = 模组列表数据Ex.FirstOrDefault(m => m.Name == mod);
                            if (matchMod != null)
                                模组列表.SelectedItem = matchMod;
                            return;
                        }
                    }
                }
            }
            // 2. 再模组分类
            foreach (var game in 游戏列表数据)
            {
                var catRoot = System.IO.Path.Combine(备份路径缓存, "游戏", game);
                if (!Directory.Exists(catRoot)) continue;
                foreach (var cat in Directory.GetDirectories(catRoot).Select(p => System.IO.Path.GetFileName(p)))
                {
                    if (cat.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        游戏列表.SelectedItem = game;
                        刷新模组分类(game);
                        模组分类.SelectedItem = cat;
                        刷新模组列表(game, cat);
                        return;
                    }
                }
            }
            // 3. 最后游戏列表
            var gameMatch = 游戏列表数据.FirstOrDefault(g => g.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
            if (gameMatch != null)
            {
                游戏列表.SelectedItem = gameMatch;
                刷新模组分类(gameMatch);
                return;
            }
        }

        // 游戏列表右键
        private void 游戏列表_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = GetListBoxItemUnderMouse(游戏列表, e);
            if (item == null) return;
            游戏列表.SelectedItem = item;
            _rightClickTarget = item;
            ShowContextMenu(e.GetPosition(this), "游戏");
        }

        // 模组分类右键
        private void 模组分类_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = GetListBoxItemUnderMouse(模组分类, e);
            if (item == null) return;
            模组分类.SelectedItem = item;
            _rightClickTarget = item;
            ShowContextMenu(e.GetPosition(this), "分类");
        }

        // 模组列表右键
        private void 模组列表_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = GetListBoxItemUnderMouse(模组列表, e);
            if (item == null) return;
            模组列表.SelectedItem = item;
            _rightClickTarget = item;
            ShowContextMenu(e.GetPosition(this), "模组");
        }

        private object GetListBoxItemUnderMouse(ListBox listBox, MouseButtonEventArgs e)
        {
            var point = e.GetPosition(listBox);
            var element = listBox.InputHitTest(point) as DependencyObject;
            while (element != null && !(element is ListBoxItem))
                element = VisualTreeHelper.GetParent(element);
            return (element as ListBoxItem)?.DataContext ?? (element as ListBoxItem)?.Content;
        }

        private void ShowContextMenu(Point pos, string type)
        {
            var menu = new ContextMenu();
            var rename = new MenuItem { Header = "重命名", FontSize = 16 };
            var del = new MenuItem { Header = "删除", FontSize = 16 };
            rename.Click += (s, e) => OnRename(type);
            del.Click += (s, e) => OnDelete(type);
            menu.Items.Add(rename);
            menu.Items.Add(del);
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
            menu.PlacementTarget = this;
            menu.HorizontalOffset = pos.X;
            menu.VerticalOffset = pos.Y;
            menu.IsOpen = true;
        }

        private void OnRename(string type)
        {
            string oldName = _rightClickTarget is ModItem mi ? mi.Name : _rightClickTarget?.ToString();
            var win = new 命名窗口();
            win.Owner = this;
            win.命名输入.Text = oldName;
            win.命名输入.SelectAll();
            if (win.ShowDialog() == true)
            {
                string newName = win.命名结果;
                if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;
                if (type == "游戏")
                {
                    RenameGameFolder(oldName, newName);
                }
                else if (type == "分类")
                {
                    RenameCategoryFolder(oldName, newName);
                }
                else if (type == "模组")
                {
                    RenameModFolder(oldName, newName);
                }
            }
        }

        private void OnDelete(string type)
        {
            string name = _rightClickTarget is ModItem mi ? mi.Name : _rightClickTarget?.ToString();
            if (string.IsNullOrWhiteSpace(name)) return;
            if (提示窗口_确认删除($"确定要删除{name}吗？"))
            {
                if (type == "游戏")
                    DeleteGameFolder(name);
                else if (type == "分类")
                    DeleteCategoryFolder(name);
                else if (type == "模组")
                    DeleteModFolder(name);
            }
        }

        private bool 提示窗口_确认删除(string msg)
        {
            return 提示窗口.ShowConfirm(msg, this);
        }

        private void RenameGameFolder(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(备份路径缓存)) return;
            var oldDir = System.IO.Path.Combine(备份路径缓存, "游戏", oldName);
            var newDir = System.IO.Path.Combine(备份路径缓存, "游戏", newName);
            if (Directory.Exists(newDir))
            {
                提示窗口.Show("新名称已存在！", this);
                return;
            }
            Directory.Move(oldDir, newDir);
            // 配置文件同步
            if (当前数据["games"] is JArray games)
            {
                var game = games.FirstOrDefault(g => g["name"]?.ToString() == oldName);
                if (game != null) game["name"] = newName;
            }
            保存配置();
            刷新游戏列表();
            游戏列表.SelectedItem = newName;
        }

        private void RenameCategoryFolder(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(备份路径缓存) || 游戏列表.SelectedItem == null) return;
            var game = 游戏列表.SelectedItem.ToString();
            var oldDir = System.IO.Path.Combine(备份路径缓存, "游戏", game, oldName);
            var newDir = System.IO.Path.Combine(备份路径缓存, "游戏", game, newName);
            if (Directory.Exists(newDir))
            {
                提示窗口.Show("新名称已存在！", this);
                return;
            }
            Directory.Move(oldDir, newDir);
            刷新模组分类(game);
            模组分类.SelectedItem = newName;
        }

        private void RenameModFolder(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(备份路径缓存) || 游戏列表.SelectedItem == null || 模组分类.SelectedItem == null) return;
            var game = 游戏列表.SelectedItem.ToString();
            var cat = 模组分类.SelectedItem.ToString();
            var oldDir = System.IO.Path.Combine(备份路径缓存, "游戏", game, cat, oldName);
            var newDir = System.IO.Path.Combine(备份路径缓存, "游戏", game, cat, newName);
            if (Directory.Exists(newDir))
            {
                提示窗口.Show("新名称已存在！", this);
                return;
            }
            Directory.Move(oldDir, newDir);
            刷新模组列表(game, cat);
            var match = 模组列表数据Ex.FirstOrDefault(m => m.Name == newName);
            if (match != null) 模组列表.SelectedItem = match;
        }

        private void DeleteGameFolder(string name)
        {
            var dir = System.IO.Path.Combine(备份路径缓存, "游戏", name);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            if (当前数据["games"] is JArray games)
            {
                var game = games.FirstOrDefault(g => g["name"]?.ToString() == name);
                if (game != null) games.Remove(game);
            }
            保存配置();
            刷新游戏列表();
            游戏列表.SelectedItem = null;
            // 清空模组分类和模组列表
            模组分类数据.Clear();
            模组分类.SelectedItem = null;
            模组列表数据.Clear();
            模组列表数据Ex.Clear();
            模组列表.SelectedItem = null;
        }

        private void DeleteCategoryFolder(string name)
        {
            if (游戏列表.SelectedItem == null) return;
            var game = 游戏列表.SelectedItem.ToString();
            var dir = System.IO.Path.Combine(备份路径缓存, "游戏", game, name);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            刷新模组分类(game);
            模组分类.SelectedItem = null;
        }

        private void DeleteModFolder(string name)
        {
            if (游戏列表.SelectedItem == null || 模组分类.SelectedItem == null) return;
            var game = 游戏列表.SelectedItem.ToString();
            var cat = 模组分类.SelectedItem.ToString();
            var dir = System.IO.Path.Combine(备份路径缓存, "游戏", game, cat, name);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            刷新模组列表(game, cat);
            模组列表.SelectedItem = null;
        }
    }

    public class ModItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged("IsEnabled"); }
        }
        // 记录复制到目标的文件夹名（如有重名加后缀）
        public List<string> CopiedFolderNames { get; set; } = new List<string>();
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
