using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace EdgeSecurityAccessDesktop
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient client = new HttpClient();
        private readonly string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "esac.conf");

        public MainWindow()
        {
            InitializeComponent();
            this.Closing += MainWindow_Closing;
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlBox.Text.Trim();
            string username = UserBox.Text.Trim();
            string password = PassBox.Password;

            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http"))
            {
                StatusText.Text = "请输入有效的 API URL (以 http/https 开头)";
                return;
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                StatusText.Text = "请填写用户名和密码";
                return;
            }

            var formData = new Dictionary<string, string>
            {
                { "username", username },
                { "password", password }
            };

            try
            {
                StatusText.Text = "正在连接服务器...";
                var content = new FormUrlEncodedContent(formData);
                client.Timeout = TimeSpan.FromSeconds(15);
                var response = await client.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    StatusText.Text = $"请求失败: {response.StatusCode}";
                    return;
                }

                string result = await response.Content.ReadAsStringAsync();

                if (!result.Contains("[Interface]"))
                {
                    StatusText.Text = "验证成功，但返回的内容不是有效的配置";
                    return;
                }

                await File.WriteAllTextAsync(configPath, result);
                StatusText.Text = "配置已获取，正在应用网络更改...";
                
                bool isSuccess = await Task.Run(() => SetupWireGuard());

                if (isSuccess)
                {
                    StatusText.Text = "连接成功：加密隧道已开启";
                }
                else
                {
                    StatusText.Text = "错误：无法启动 wg-quick。请检查管理员权限或是否安装了 WireGuard。";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "发生异常: " + ex.Message;
            }
        }
        
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            RunWgCommand("down");
        }

        private bool SetupWireGuard()
        {
            try
            {
                RunWgCommand("down");
                return RunWgCommand("up");
            }
            catch
            {
                return false;
            }
        }

        private bool RunWgCommand(string action)
        {
            if (!File.Exists(configPath)) return false;

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "wg-quick",
                Arguments = $"{action} \"{configPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            };
            try
            {
                using (Process proc = Process.Start(psi))
                {
                    if (proc == null) return false;
                    proc.WaitForExit();
                    return proc.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}