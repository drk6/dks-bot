using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ENIApp
{
    class Program
    {
        public static string GitHubUser = "drk6";
        public static string GitHubRepo = ".exe-app";
        public static string GitHubToken = "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";
        public static string CurrentVersion = "1.0.12";

        [STAThread]
        static void Main(string[] args)
        {
            // Handle --install-update flag for Discord-style self-update
            if (args.Length > 0 && args[0] == "--install-update")
            {
                string currentPath = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(currentPath))
                    currentPath = Process.GetCurrentProcess().MainModule.FileName;
                InstallUpdateAndRestart(currentPath);
                return;
            }

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string myPath = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(myPath))
                myPath = Process.GetCurrentProcess().MainModule.FileName;
            string dir = Path.GetDirectoryName(myPath);
            string mainExe = Path.Combine(dir, "app.exe");
            string newExe = Path.Combine(dir, "app_new.exe");

            // Clean up any leftover temp files from previous failed updates
            try
            {
                if (File.Exists(newExe)) File.Delete(newExe);
                string oldFile = Path.Combine(dir, "app.old.exe");
                string vbsFile = Path.Combine(dir, "update.vbs");
                if (File.Exists(oldFile)) File.Delete(oldFile);
                string vbsFile2 = Path.Combine(dir, "update.bat");
                if (File.Exists(vbsFile2)) File.Delete(vbsFile2);
            }
            catch { }

            if (CheckForUpdate())
                return;

            Application.Run(new MainForm());
        }

public static bool CheckForUpdate()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "ENI-App");
                    client.DefaultRequestHeaders.Add("Authorization", "token " + GitHubToken);
                    client.Timeout = TimeSpan.FromSeconds(15);

                    string versionUrl = "https://api.github.com/repos/" + GitHubUser + "/" + GitHubRepo + "/contents/version.txt";
                    string versionResp = client.GetStringAsync(versionUrl).GetAwaiter().GetResult();
                    Dictionary<string,object> versionJson = (Dictionary<string,object>)ParseJson(versionResp);
                    string content = Encoding.UTF8.GetString(Convert.FromBase64String(versionJson["content"].ToString().Replace("\n", ""))).Trim();
                    
                    string[] parts = content.Split('|');
                    string latestVersion = parts[0].Trim();
                    bool forceUpdate = parts.Length > 1 && parts[1].Trim().ToLower() == "true";

                    if (latestVersion != CurrentVersion)
                    {
                        if (forceUpdate)
                        {
                            DownloadUpdateSilent();
                            return true;
                        }
                        // force=false: silently ignore, no prompt
                        return false;
                    }
                }
            }
            catch { }
            return false;
        }

        public static void DownloadUpdateSilent()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "ENI-App");
                    client.DefaultRequestHeaders.Add("Authorization", "token " + GitHubToken);
                    client.Timeout = TimeSpan.FromMinutes(5);

                    string exeUrl = "https://api.github.com/repos/" + GitHubUser + "/" + GitHubRepo + "/contents/app.exe";
                    string exeResp = client.GetStringAsync(exeUrl).GetAwaiter().GetResult();
                    Dictionary<string,object> exeJson = (Dictionary<string,object>)ParseJson(exeResp);
                    byte[] exeBytes = Convert.FromBase64String(exeJson["content"].ToString().Replace("\n", ""));

                    string currentPath = Assembly.GetExecutingAssembly().Location;
                    if (string.IsNullOrEmpty(currentPath))
                        currentPath = Process.GetCurrentProcess().MainModule.FileName;
                    string dir = Path.GetDirectoryName(currentPath);
                    string newPath = Path.Combine(dir, "app_new.exe");

                    File.WriteAllBytes(newPath, exeBytes);

                    // Signal UI that update is ready
                    if (UpdateReady != null)
                        UpdateReady();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Update download failed: " + ex.Message, "ENI Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Called when app is launched with --install-update
        public static void InstallUpdateAndRestart(string currentPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(currentPath);
                string newPath = Path.Combine(dir, "app_new.exe");
                string oldPath = Path.Combine(dir, "app_old.exe");

                // Wait for old process to fully exit
                Thread.Sleep(2000);

                // Robust cleanup with retries
                for (int i = 0; i < 10; i++)
                {
                    try { if (File.Exists(oldPath)) File.Delete(oldPath); break; }
                    catch { Thread.Sleep(500); }
                }

                for (int i = 0; i < 10; i++)
                {
                    try { if (File.Exists(currentPath)) File.Move(currentPath, oldPath); break; }
                    catch { Thread.Sleep(500); }
                }

                for (int i = 0; i < 10; i++)
                {
                    try { if (File.Exists(newPath)) File.Move(newPath, currentPath); break; }
                    catch { Thread.Sleep(500); }
                }

                // Cleanup old file
                for (int i = 0; i < 10; i++)
                {
                    try { if (File.Exists(oldPath)) File.Delete(oldPath); break; }
                    catch { Thread.Sleep(500); }
                }

                Process.Start(currentPath);
            }
            catch { }
            finally
            {
                Environment.Exit(0);
}
        }

        public static Action UpdateReady;

        public static object ParseJson(string json)
        {
            return new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(json);
        }
    }

    public class MainForm : Form
    {
        private Panel sidebar;
        private Panel content;
        private Panel activeIndicator;
        private Button activeButton;
        private Label titleLabel;
        private Label versionLabel;

        Color accentColor = Color.FromArgb(0, 200, 255);
        Color bgColor = Color.FromArgb(25, 25, 30);
        Color sidebarColor = Color.FromArgb(18, 18, 22);
        Color cardColor = Color.FromArgb(35, 35, 42);
        Color hoverColor = Color.FromArgb(40, 40, 50);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        private int circleHotkey = 0;
        private int squareHotkey = 0;
        private int circleWaitMs = 1500;
        private int squareWaitMs = 1500;
        private double circleSpeedMs = 6.0;
        private double squareSpeedMs = 3.0;
        private int circlePendingKey = 0;
        private int squarePendingKey = 0;
        private Thread hotkeyThread;
        private bool running = true;
        private bool isLoggedIn = false;
        private string loggedInUser = "";
        private string loginToken = "";
        private Button devButton;
        private string settingsPath;

        public MainForm()
        {
            settingsPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "settings.txt");
            LoadSettings();

            sidebar = new Panel
            {
                Width = 220,
                Dock = DockStyle.Left,
                BackColor = sidebarColor
            };

            titleLabel = new Label
            {
                Text = "E N I",
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = accentColor,
                Location = new Point(30, 25),
                AutoSize = true
            };

            versionLabel = new Label
            {
                Text = "v" + Program.CurrentVersion,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(80, 80, 90),
                Location = new Point(32, 62),
                AutoSize = true
            };

            activeIndicator = new Panel
            {
                Size = new Size(3, 40),
                BackColor = accentColor,
                Location = new Point(0, 100)
            };

            Button btnHome = CreateSidebarButton("Home", 100);
            Button btnCircle = CreateSidebarButton("Circle Draw", 150);
            Button btnSquare = CreateSidebarButton("Square Draw", 200);
            devButton = CreateSidebarButton("Dev", 250);
            Button btnLogin = CreateSidebarButton("Login", 520);

            btnHome.Click += (s, e) => ShowPage("home");
            btnCircle.Click += (s, e) => ShowPage("circle");
            btnSquare.Click += (s, e) => ShowPage("square");
            devButton.Click += (s, e) => ShowPage("dev");
            btnLogin.Click += (s, e) => ShowPage("login");

            devButton.Visible = false;

            sidebar.Controls.AddRange(new Control[] { titleLabel, versionLabel, activeIndicator, btnHome, btnCircle, btnSquare, devButton, btnLogin });

            content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = bgColor,
                Margin = new Padding(220, 0, 0, 0),
                AutoScroll = true
            };

            Controls.Add(content);
            Controls.Add(sidebar);

            Text = "ENI App";
            Size = new Size(950, 650);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = bgColor;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 10);
            FormClosing += (s, e) => { running = false; SaveSettings(); };

            hotkeyThread = new Thread(HotkeyLoop);
            hotkeyThread.IsBackground = true;
            hotkeyThread.Start();

            Thread updateThread = new Thread(BackgroundUpdateCheck);
            updateThread.IsBackground = true;
            updateThread.Start();

            if (isLoggedIn)
            {
                CheckDevStatus();
            }

            // Subscribe to update ready event
            Program.UpdateReady += OnUpdateReady;

            ShowPage("home");
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    string[] lines = File.ReadAllLines(settingsPath);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split(new char[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string val = parts[1].Trim();
                            switch (key)
                            {
                                case "circleHotkey": int.TryParse(val, out circleHotkey); circlePendingKey = circleHotkey; break;
                                case "squareHotkey": int.TryParse(val, out squareHotkey); squarePendingKey = squareHotkey; break;
                                case "circleWaitMs": int.TryParse(val, out circleWaitMs); break;
                                case "squareWaitMs": int.TryParse(val, out squareWaitMs); break;
                                case "circleSpeedMs": double.TryParse(val, out circleSpeedMs); break;
                                case "squareSpeedMs": double.TryParse(val, out squareSpeedMs); break;
                                case "loginToken": loginToken = val; break;
                                case "isLoggedIn": bool.TryParse(val, out isLoggedIn); break;
                                case "loggedInUser": loggedInUser = val; break;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                string content_str =
                    "circleHotkey=" + circleHotkey + "\r\n" +
                    "squareHotkey=" + squareHotkey + "\r\n" +
                    "circleWaitMs=" + circleWaitMs + "\r\n" +
                    "squareWaitMs=" + squareWaitMs + "\r\n" +
                    "circleSpeedMs=" + circleSpeedMs + "\r\n" +
                    "squareSpeedMs=" + squareSpeedMs + "\r\n" +
                    "loginToken=" + loginToken + "\r\n" +
                    "isLoggedIn=" + isLoggedIn + "\r\n" +
                    "loggedInUser=" + loggedInUser + "\r\n";
                File.WriteAllText(settingsPath, content_str);
            }
            catch { }
        }

        private void BackgroundUpdateCheck()
        {
            while (running)
            {
                Thread.Sleep(60000);
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "ENI-App");
                        client.DefaultRequestHeaders.Add("Authorization", "token " + Program.GitHubToken);
                        client.Timeout = TimeSpan.FromSeconds(15);

                        string url = "https://api.github.com/repos/" + Program.GitHubUser + "/" + Program.GitHubRepo + "/contents/version.txt";
                        string resp = client.GetStringAsync(url).GetAwaiter().GetResult();
                        Dictionary<string,object> json = (Dictionary<string,object>)Program.ParseJson(resp);
                        string content = Encoding.UTF8.GetString(Convert.FromBase64String(json["content"].ToString().Replace("\n", ""))).Trim();
                        
                        string[] parts = content.Split('|');
                        string latest = parts[0].Trim();
                        bool forceUpdate = parts.Length > 1 && parts[1].Trim().ToLower() == "true";

                        if (latest != Program.CurrentVersion)
                        {
                            if (forceUpdate)
                            {
                                try { this.Invoke(new Action(() => { Program.DownloadUpdateSilent(); })); } catch { }
                            }
                            // force=false: silently ignore
                        }
                    }
                }
                catch { }
            }
        }

        private void CheckDevStatus()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "ENI-App");
                    client.DefaultRequestHeaders.Add("Authorization", "token " + loginToken);
                    string resp = client.GetStringAsync("https://api.github.com/user").GetAwaiter().GetResult();
                    dynamic json = Program.ParseJson(resp);
                                string user = ((Dictionary<string,object>)json)["login"].ToString();
                    if (user == "drk6")
                    {
                        isLoggedIn = true;
                        loggedInUser = user;
                        devButton.Visible = true;
                    }
                    else
                    {
                        isLoggedIn = true;
                        loggedInUser = user;
                        devButton.Visible = false;
                    }
                }
            }
            catch
            {
                isLoggedIn = false;
                loggedInUser = "";
                devButton.Visible = false;
            }
        }

        private void HotkeyLoop()
        {
            while (running)
            {
                if (circleHotkey != 0 && GetAsyncKeyState(circleHotkey) == -32767)
                {
                    try { this.Invoke(new Action(() => RunCircleDraw())); } catch { }
                }
                if (squareHotkey != 0 && GetAsyncKeyState(squareHotkey) == -32767)
                {
                    try { this.Invoke(new Action(() => RunSquareDraw())); } catch { }
                }
                Thread.Sleep(10);
            }
        }

        private void RunCircleDraw()
        {
            POINT pos;
            GetCursorPos(out pos);
            double cx = pos.X;
            double cy = pos.Y;
            int steps = 800;
            SetCursorPos((int)(cx + 100), (int)cy);
            Thread.Sleep(100);
            mouse_event(0x02, 0, 0, 0, IntPtr.Zero);
            var sw = Stopwatch.StartNew();
            for (int i = 1; i <= steps; i++)
            {
                double angle = (2.0 * Math.PI * i) / steps;
                int x = (int)Math.Round(cx + (100 * Math.Cos(angle)));
                int y2 = (int)Math.Round(cy + (100 * Math.Sin(angle)));
                SetCursorPos(x, y2);
                double expected = i * circleSpeedMs;
                while (sw.Elapsed.TotalMilliseconds < expected) { }
            }
            SetCursorPos((int)(cx + 100), (int)cy);
            mouse_event(0x04, 0, 0, 0, IntPtr.Zero);
        }

        private void RunSquareDraw()
        {
            POINT pos;
            GetCursorPos(out pos);
            double sx = pos.X;
            double sy = pos.Y;
            int size = 300;
            int stepsPerSide = 200;
            SetCursorPos((int)sx, (int)sy);
            Thread.Sleep(100);
            mouse_event(0x02, 0, 0, 0, IntPtr.Zero);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i <= stepsPerSide; i++) { double t = (double)i / stepsPerSide; SetCursorPos((int)(sx + size * t), (int)sy); double expected = i * squareSpeedMs; while (sw.Elapsed.TotalMilliseconds < expected) { } }
            for (int i = 0; i <= stepsPerSide; i++) { double t = (double)i / stepsPerSide; SetCursorPos((int)(sx + size), (int)(sy + size * t)); double expected = (stepsPerSide + i) * squareSpeedMs; while (sw.Elapsed.TotalMilliseconds < expected) { } }
            for (int i = 0; i <= stepsPerSide; i++) { double t = (double)i / stepsPerSide; SetCursorPos((int)(sx + size - size * t), (int)(sy + size)); double expected = (stepsPerSide * 2 + i) * squareSpeedMs; while (sw.Elapsed.TotalMilliseconds < expected) { } }
            for (int i = 0; i <= stepsPerSide; i++) { double t = (double)i / stepsPerSide; SetCursorPos((int)sx, (int)(sy + size - size * t)); double expected = (stepsPerSide * 3 + i) * squareSpeedMs; while (sw.Elapsed.TotalMilliseconds < expected) { } }
            mouse_event(0x04, 0, 0, 0, IntPtr.Zero);
        }

        private Button CreateSidebarButton(string text, int y)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(220, 42),
                Location = new Point(0, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = sidebarColor,
                ForeColor = Color.FromArgb(160, 160, 170),
                Font = new Font("Segoe UI", 11),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(30, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = hoverColor;
            btn.MouseEnter += (s, e) => { if (btn != activeButton) btn.ForeColor = Color.White; };
            btn.MouseLeave += (s, e) => { if (btn != activeButton) btn.ForeColor = Color.FromArgb(160, 160, 170); };
            return btn;
        }

        private System.Windows.Forms.Timer slideTimer;
        private int slideTarget = 100;

        private void HighlightButton(Button btn)
        {
            if (activeButton != null)
            {
                activeButton.ForeColor = Color.FromArgb(160, 160, 170);
                activeButton.BackColor = sidebarColor;
            }
            activeButton = btn;
            activeButton.ForeColor = Color.White;
            slideTarget = btn.Top + 1;

            if (slideTimer == null)
            {
                slideTimer = new System.Windows.Forms.Timer();
                slideTimer.Interval = 8;
                slideTimer.Tick += SlideTick;
            }
            slideTimer.Start();
        }

        private void SlideTick(object sender, EventArgs e)
        {
            int diff = slideTarget - activeIndicator.Top;
            if (Math.Abs(diff) < 2)
            {
                activeIndicator.Top = slideTarget;
                slideTimer.Stop();
                return;
            }
            activeIndicator.Top += diff / 3;
        }

        private void ShowPage(string page)
        {
            content.Controls.Clear();
            foreach (Control c in sidebar.Controls)
            {
                if (c is Button)
                {
                    Button b = (Button)c;
                    string t = b.Text.ToLower();
                    if (t == page || t.Contains(page) || (page == "home" && b.Text == "Home") || (page == "dev" && b.Text == "Dev") || (page == "login" && b.Text == "Login"))
                    {
                        HighlightButton(b);
                        break;
                    }
                }
            }
            switch (page)
            {
                case "home": ShowHome(); break;
                case "circle": ShowCircle(); break;
                case "square": ShowSquare(); break;
                case "settings": ShowSettings(); break;
                case "dev": ShowDev(); break;
                case "login": ShowLogin(); break;
            }
        }

        private void OnUpdateReady()
        {
            try
            {
                this.Invoke(new Action(() =>
                {
                    var panel = new Panel
                    {
                        Location = new Point(40, 40),
                        Size = new Size(400, 100),
                        BackColor = Color.FromArgb(35, 35, 42)
                    };
                    var label = new Label
                    {
                        Text = "Update ready! Restart to apply.",
                        Font = new Font("Segoe UI", 12, FontStyle.Regular),
                        ForeColor = Color.White,
                        Location = new Point(15, 15),
                        AutoSize = true
                    };
                    var restartBtn = new Button
                    {
                        Text = "Restart to Update",
                        Location = new Point(15, 50),
                        Size = new Size(180, 40),
                        FlatStyle = FlatStyle.Flat,
                        BackColor = Color.FromArgb(0, 200, 255),
                        ForeColor = Color.Black,
                        Font = new Font("Segoe UI", 11, FontStyle.Bold),
                        Cursor = Cursors.Hand
                    };
                    restartBtn.FlatAppearance.BorderSize = 0;
                    restartBtn.Click += (s, e) =>
                    {
                        // Launch self with --install-update flag and exit
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = Assembly.GetExecutingAssembly().Location,
                            Arguments = "--install-update",
                            UseShellExecute = true
                        });
                        Environment.Exit(0);
                    };
                    panel.Controls.AddRange(new Control[] { label, restartBtn });
                    content.Controls.Add(panel);
                    panel.BringToFront();
                }));
            }
            catch { }
        }

        private void ShowHome()
        {
            var title = MakeLabel("Welcome to ENI App", 24, FontStyle.Bold, Color.White, 40, 30);
            var sub = MakeLabel("Your all-in-one tool hub", 12, FontStyle.Regular, Color.FromArgb(100, 100, 110), 40, 75);
            var circleCard = MakeCard("Circle Draw", "Draw a perfect circle on your desktop", 40, 130, 280, 120, new EventHandler((s, e) => ShowPage("circle")));
            var squareCard = MakeCard("Square Draw", "Draw a perfect square on your desktop", 340, 130, 280, 120, new EventHandler((s, e) => ShowPage("square")));
            content.Controls.AddRange(new Control[] { title, sub, circleCard, squareCard });
        }

        private Panel MakeCard(string title, string desc, int x, int y, int w, int h, EventHandler onClick)
        {
            var card = new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = cardColor };
            var t = new Label { Text = title, Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = accentColor, Location = new Point(15, 15), AutoSize = true, BackColor = Color.Transparent };
            var d = new Label { Text = desc, Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(120, 120, 130), Location = new Point(15, 50), Size = new Size(w - 30, 40), BackColor = Color.Transparent };
            card.Click += onClick;
            t.Click += onClick;
            d.Click += onClick;
            card.Controls.AddRange(new Control[] { t, d });
            card.Cursor = Cursors.Hand;
            card.MouseEnter += (s, e) => card.BackColor = hoverColor;
            card.MouseLeave += (s, e) => card.BackColor = cardColor;
            return card;
        }

        private void ShowCircle()
        {
            var title = MakeLabel("Circle Draw", 24, FontStyle.Bold, Color.White, 40, 30);
            var sub = MakeLabel("Click hotkey box, press a key, then Set", 11, FontStyle.Regular, Color.FromArgb(100, 100, 110), 40, 70);

            int y = 120;
            int gap = 45;

            var hotkeyLabel = MakeLabel("Hotkey:", 11, FontStyle.Regular, Color.FromArgb(160, 160, 170), 40, y);
            var hotkeyBox = MakeHotkeyBox(circlePendingKey, y, (key) => { circlePendingKey = key; });

            var setBtn = MakeSmallButton("Set", 270, y - 5, (s, e) => { circleHotkey = circlePendingKey; SaveSettings(); });
            var clearBtn = MakeSmallButton("Clear", 335, y - 5, Color.FromArgb(160, 160, 170), (s, e) => { circleHotkey = 0; circlePendingKey = 0; hotkeyBox.Text = "None"; SaveSettings(); });

            var waitLabel = MakeLabel("Wait (sec):", 11, FontStyle.Regular, Color.FromArgb(160, 160, 170), 40, y + gap);
            var waitBox = MakeInputBox((circleWaitMs / 1000.0).ToString(), 160, y + gap);

            var speedLabel = MakeLabel("Speed (ms):", 11, FontStyle.Regular, Color.FromArgb(160, 160, 170), 40, y + gap * 2);
            var speedBox = MakeInputBox(circleSpeedMs.ToString(), 160, y + gap * 2);

            var radiusLabel = MakeLabel("Radius (px):", 11, FontStyle.Regular, Color.FromArgb(160, 160, 170), 40, y + gap * 3);
            var radiusBox = MakeInputBox("100", 160, y + gap * 3);

            var statusLabel = MakeLabel("", 11, FontStyle.Regular, accentColor, 40, y + gap * 4 + 20);
            statusLabel.Size = new Size(600, 30);

            var startBtn = new Button
            {
                Text = "Start Drawing",
                Location = new Point(40, y + gap * 4 + 55),
                Size = new Size(220, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = accentColor,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            startBtn.FlatAppearance.BorderSize = 0;

            startBtn.Click += (s, e) =>
            {
                double waitSec = 1.5;
                double.TryParse(waitBox.Text, out waitSec);
                double spd = 6;
                double.TryParse(speedBox.Text, out spd);
                int rad = 100;
                int.TryParse(radiusBox.Text, out rad);
                circleWaitMs = (int)(waitSec * 1000);
                circleSpeedMs = spd;
                SaveSettings();

                statusLabel.ForeColor = accentColor;
                statusLabel.Text = "Get ready... move cursor!";
                content.Refresh();
                Thread.Sleep(circleWaitMs);

                POINT pos;
                GetCursorPos(out pos);
                double cx = pos.X;
                double cy = pos.Y;
                statusLabel.Text = "Drawing circle...";
                content.Refresh();

                int steps = 800;
                SetCursorPos((int)(cx + rad), (int)cy);
                Thread.Sleep(100);
                mouse_event(0x02, 0, 0, 0, IntPtr.Zero);
                var sw = Stopwatch.StartNew();
                for (int i = 1; i <= steps; i++)
                {
                    double angle = (2.0 * Math.PI * i) / steps;
                    int x = (int)Math.Round(cx + (rad * Math.Cos(angle)));
                    int y2 = (int)Math.Round(cy + (rad * Math.Sin(angle)));
                    SetCursorPos(x, y2);
                    double expected = i * circleSpeedMs;
                    while (sw.Elapsed.TotalMilliseconds < expected) { }
                }
                SetCursorPos((int)(cx + rad), (int)cy);
                mouse_event(0x04, 0, 0, 0, IntPtr.Zero);
                statusLabel.ForeColor = Color.FromArgb(0, 255, 100);
                statusLabel.Text = "Circle drawn!";
            };

            content.Controls.AddRange(new Control[] { title, sub, hotkeyLabel, hotkeyBox, setBtn, clearBtn, waitLabel, waitBox, speedLabel, speedBox, radiusLabel, radiusBox, startBtn, statusLabel });
        }

        private void ShowSquare()
        {
            var title = MakeLabel("Square Draw", 24, FontStyle.Bold, Color.White, 40, 30);
            var sub = MakeLabel("Click hotkey box, press a key, then Set", 11, FontStyle.Regular, Color.FromArgb(100, 100, 110), 40, 70);

            int y = 120;
            int gap = 45;

            var hotkeyLabel = MakeLabel("Hotkey:", 11, FontStyle.Regular, Color.FromArgb(160, 160, 170), 40, y);
            var hotkeyBox = MakeHotkeyBox(squarePendingKey, y, (key) => { squarePendingKey = key; });

            var setBtn = MakeSmallButton("Set", 270, y - 5, (s, e) => { squareHotkey = squarePendingKey; SaveSettings(); });
            var clearBtn = MakeSmallButton("Clear", 335, y - 5, Color.FromArgb(160, 160, 170), (s, e) => { squareHotkey = 0; squarePendingKey = 0; hotkeyBox.Text = "None"; SaveSettings(); });

            var waitLabel = MakeLabel("Wait (sec):", 11, FontStyle.Regular, Color.FromArgb(160, 160, 170), 40, y + gap);
            var waitBox = MakeInputBox((squareWaitMs / 1000.0).ToString(), 160, y + gap);

            var speedLabel = MakeLabel("Speed (ms):", 11, FontStyle.Regular, Color.FromArgb(160, 160, 170), 40, y + gap * 2);
            var speedBox = MakeInputBox(squareSpeedMs.ToString(), 160, y + gap * 2);

            var sizeLabel = MakeLabel("Size (px):", 11, FontStyle.Regular, Color.FromArgb(160, 160, 170), 40, y + gap * 3);
            var sizeBox = MakeInputBox("300", 160, y + gap * 3);

            var statusLabel = MakeLabel("", 11, FontStyle.Regular, accentColor, 40, y + gap * 4 + 20);
            statusLabel.Size = new Size(600, 30);

            var startBtn = new Button
            {
                Text = "Start Drawing",
                Location = new Point(40, y + gap * 4 + 55),
                Size = new Size(220, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = accentColor,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            startBtn.FlatAppearance.BorderSize = 0;

            startBtn.Click += (s, e) =>
            {
                double waitSec = 1.5;
                double.TryParse(waitBox.Text, out waitSec);
                double spd = 3;
                double.TryParse(speedBox.Text, out spd);
                int sz = 300;
                int.TryParse(sizeBox.Text, out sz);
                squareWaitMs = (int)(waitSec * 1000);
                squareSpeedMs = spd;
                SaveSettings();

                statusLabel.ForeColor = accentColor;
                statusLabel.Text = "Get ready... move cursor!";
                content.Refresh();
                Thread.Sleep(squareWaitMs);

                POINT pos;
                GetCursorPos(out pos);
                double sx = pos.X;
                double sy = pos.Y;
                statusLabel.Text = "Drawing square...";
                content.Refresh();

                int stepsPerSide = 200;
                SetCursorPos((int)sx, (int)sy);
                Thread.Sleep(100);
                mouse_event(0x02, 0, 0, 0, IntPtr.Zero);
                var sw = Stopwatch.StartNew();
                for (int i = 0; i <= stepsPerSide; i++) { double t = (double)i / stepsPerSide; SetCursorPos((int)(sx + sz * t), (int)sy); double expected = i * squareSpeedMs; while (sw.Elapsed.TotalMilliseconds < expected) { } }
                for (int i = 0; i <= stepsPerSide; i++) { double t = (double)i / stepsPerSide; SetCursorPos((int)(sx + sz), (int)(sy + sz * t)); double expected = (stepsPerSide + i) * squareSpeedMs; while (sw.Elapsed.TotalMilliseconds < expected) { } }
                for (int i = 0; i <= stepsPerSide; i++) { double t = (double)i / stepsPerSide; SetCursorPos((int)(sx + sz - sz * t), (int)(sy + sz)); double expected = (stepsPerSide * 2 + i) * squareSpeedMs; while (sw.Elapsed.TotalMilliseconds < expected) { } }
                for (int i = 0; i <= stepsPerSide; i++) { double t = (double)i / stepsPerSide; SetCursorPos((int)sx, (int)(sy + sz - sz * t)); double expected = (stepsPerSide * 3 + i) * squareSpeedMs; while (sw.Elapsed.TotalMilliseconds < expected) { } }
                mouse_event(0x04, 0, 0, 0, IntPtr.Zero);
                statusLabel.ForeColor = Color.FromArgb(0, 255, 100);
                statusLabel.Text = "Square drawn!";
            };

            content.Controls.AddRange(new Control[] { title, sub, hotkeyLabel, hotkeyBox, setBtn, clearBtn, waitLabel, waitBox, speedLabel, speedBox, sizeLabel, sizeBox, startBtn, statusLabel });
        }

        private TextBox MakeHotkeyBox(int currentKey, int yPos, Action<int> onKeySet)
        {
            var box = new TextBox
            {
                Location = new Point(160, yPos - 3),
                Size = new Size(100, 30),
                BackColor = cardColor,
                ForeColor = accentColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11),
                Text = currentKey != 0 ? ((Keys)currentKey).ToString() : "None"
            };

            box.Enter += (s, e) => { box.SelectAll(); };

            box.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.None && e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.Menu && e.KeyCode != Keys.LWin && e.KeyCode != Keys.RWin)
                {
                    onKeySet((int)e.KeyCode);
                    box.Text = e.KeyCode.ToString();
                    e.SuppressKeyPress = true;
                }
            };

            box.KeyPress += (s, e) => { e.Handled = true; };

            return box;
        }

        private Button MakeSmallButton(string text, int x, int y, EventHandler onClick)
        {
            return MakeSmallButton(text, x, y, Color.White, onClick);
        }

        private Button MakeSmallButton(string text, int x, int y, Color foreColor, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(60, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = hoverColor,
                ForeColor = foreColor,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            return btn;
        }

        private TextBox MakeInputBox(string text, int x, int y)
        {
            return new TextBox
            {
                Location = new Point(x, y - 3),
                Size = new Size(100, 30),
                BackColor = cardColor,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11),
                Text = text
            };
        }

        private void ShowLogin()
        {
            var title = MakeLabel("Login", 24, FontStyle.Bold, Color.White, 40, 30);

            if (isLoggedIn)
            {
                var info = MakeLabel("Logged in as: " + loggedInUser, 14, FontStyle.Bold, accentColor, 40, 80);
                var devInfo = MakeLabel(loggedInUser == "drk6" ? "Dev access: Enabled" : "Dev access: Disabled", 11, FontStyle.Regular, Color.FromArgb(120, 120, 130), 40, 115);

                var logoutBtn = new Button
                {
                    Text = "Logout",
                    Location = new Point(40, 160),
                    Size = new Size(150, 40),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(200, 50, 50),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 11),
                    Cursor = Cursors.Hand
                };
                logoutBtn.FlatAppearance.BorderSize = 0;
                logoutBtn.Click += (s, e) =>
                {
                    isLoggedIn = false;
                    loggedInUser = "";
                    loginToken = "";
                    devButton.Visible = false;
                    try
                    {
                        string ghLogout = "C:\\Program Files\\GitHub CLI\\gh.exe";
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = ghLogout,
                            Arguments = "auth logout --hostname github.com",
                            WindowStyle = ProcessWindowStyle.Hidden,
                            CreateNoWindow = true
                        });
                    }
                    catch { }
                    SaveSettings();
                    ShowPage("login");
                };

                content.Controls.AddRange(new Control[] { title, info, devInfo, logoutBtn });
            }
            else
            {
                var info = MakeLabel("Click below to login with GitHub in your browser", 11, FontStyle.Regular, Color.FromArgb(120, 120, 130), 40, 80);
                var info2 = MakeLabel("You may have to press login twice", 11, FontStyle.Regular, Color.FromArgb(80, 80, 90), 40, 105);

                var loginBtn = new Button
                {
                    Text = "Login with GitHub",
                    Location = new Point(40, 155),
                    Size = new Size(250, 50),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = accentColor,
                    ForeColor = Color.Black,
                    Font = new Font("Segoe UI", 13, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                loginBtn.FlatAppearance.BorderSize = 0;

                var statusLabel = MakeLabel("", 11, FontStyle.Regular, accentColor, 40, 225);
                statusLabel.Size = new Size(600, 60);

                loginBtn.Click += (s, e) =>
                {
                    loginBtn.Enabled = false;
                    statusLabel.ForeColor = accentColor;
                    statusLabel.Text = "Checking GitHub login...";
                    content.Refresh();

                    Thread loginThread = new Thread(() =>
                    {
                        try
                        {
                            string ghPath = "C:\\Program Files\\GitHub CLI\\gh.exe";

                            Process checkProcess = new Process();
                            checkProcess.StartInfo.FileName = ghPath;
                            checkProcess.StartInfo.Arguments = "auth token --hostname github.com";
                            checkProcess.StartInfo.UseShellExecute = false;
                            checkProcess.StartInfo.RedirectStandardOutput = true;
                            checkProcess.StartInfo.CreateNoWindow = true;
                            checkProcess.Start();
                            string existingToken = checkProcess.StandardOutput.ReadToEnd().Trim();
                            checkProcess.WaitForExit();

                            bool hasValidToken = !string.IsNullOrEmpty(existingToken) && (existingToken.StartsWith("ghp_") || existingToken.StartsWith("gho_"));

                            if (!hasValidToken)
                            {
                                try { this.Invoke(new Action(() =>
                                {
                                    statusLabel.ForeColor = accentColor;
                                    statusLabel.Text = "Authorize in browser, then keep the cmd window open. Press Enter there if prompted.";
                                    content.Refresh();
                                })); } catch { }

                                Process ghProcess = new Process();
                                ghProcess.StartInfo.FileName = "cmd.exe";
                                ghProcess.StartInfo.Arguments = "/c \"\"C:\\Program Files\\GitHub CLI\\gh.exe\" auth login --hostname github.com --git-protocol https --web\"";
                                ghProcess.StartInfo.UseShellExecute = true;
                                ghProcess.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                                ghProcess.Start();

                                for (int attempt = 0; attempt < 60; attempt++)
                                {
                                    Thread.Sleep(2000);

                                    Process tokenProcess = new Process();
                                    tokenProcess.StartInfo.FileName = ghPath;
                                    tokenProcess.StartInfo.Arguments = "auth token --hostname github.com";
                                    tokenProcess.StartInfo.UseShellExecute = false;
                                    tokenProcess.StartInfo.RedirectStandardOutput = true;
                                    tokenProcess.StartInfo.CreateNoWindow = true;
                                    tokenProcess.Start();
                                    string token = tokenProcess.StandardOutput.ReadToEnd().Trim();
                                    tokenProcess.WaitForExit();

                                    if (!string.IsNullOrEmpty(token) && (token.StartsWith("ghp_") || token.StartsWith("gho_")))
                                    {
                                        existingToken = token;
                                        break;
                                    }
                                }

                                if (string.IsNullOrEmpty(existingToken) || (!existingToken.StartsWith("ghp_") && !existingToken.StartsWith("gho_")))
                                {
                                    try { this.Invoke(new Action(() =>
                                    {
                                        statusLabel.ForeColor = accentColor;
                                        statusLabel.Text = "Authorized! Click Login again.";
                                        loginBtn.Enabled = true;
                                    })); } catch { }
                                    return;
                                }
                            }

                            loginToken = existingToken;

                            using (HttpClient client = new HttpClient())
                            {
                                client.DefaultRequestHeaders.Add("User-Agent", "ENI-App");
                                client.DefaultRequestHeaders.Add("Authorization", "token " + loginToken);
                                string resp = client.GetStringAsync("https://api.github.com/user").GetAwaiter().GetResult();
                                Dictionary<string,object> json = (Dictionary<string,object>)Program.ParseJson(resp);
                                string user = json["login"].ToString();
                                isLoggedIn = true;
                                loggedInUser = user;
                                SaveSettings();

                                try { this.Invoke(new Action(() =>
                                {
                                    if (user == "drk6")
                                    {
                                        devButton.Visible = true;
                                        statusLabel.ForeColor = Color.FromArgb(0, 255, 100);
                                        statusLabel.Text = "Logged in as " + user + " (Dev)";
                                    }
                                    else
                                    {
                                        statusLabel.ForeColor = Color.FromArgb(0, 255, 100);
                                        statusLabel.Text = "Logged in as " + user;
                                    }
                                    loginBtn.Enabled = true;
                                })); } catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            try { this.Invoke(new Action(() =>
                            {
                                statusLabel.ForeColor = Color.Red;
                                statusLabel.Text = "Error: " + ex.Message;
                                loginBtn.Enabled = true;
                            })); } catch { }
                        }
                    });
                    loginThread.IsBackground = true;
                    loginThread.Start();
                };

                content.Controls.AddRange(new Control[] { title, info, info2, loginBtn, statusLabel });
            }
        }

        private void ShowDev()
        {
            if (loggedInUser != "drk6")
            {
                var noAccess = MakeLabel("Dev access denied", 24, FontStyle.Bold, Color.Red, 40, 30);
                content.Controls.Add(noAccess);
                return;
            }

            var title = MakeLabel("Dev Panel", 24, FontStyle.Bold, accentColor, 40, 30);
            var info = MakeLabel("Logged in as: " + loggedInUser, 11, FontStyle.Regular, Color.FromArgb(120, 120, 130), 40, 75);

            var statusLabel = MakeLabel("", 11, FontStyle.Regular, accentColor, 40, 350);
            statusLabel.Size = new Size(600, 100);

            var forceUpdateBtn = new Button
            {
                Text = "Force Update - Bump Version",
                Location = new Point(40, 130),
                Size = new Size(300, 45),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(200, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            forceUpdateBtn.FlatAppearance.BorderSize = 0;

            var bumpLabel = MakeLabel("New version number:", 11, FontStyle.Regular, Color.FromArgb(160, 160, 170), 40, 195);
            var bumpBox = new TextBox
            {
                Location = new Point(190, 192),
                Size = new Size(150, 30),
                BackColor = cardColor,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11),
                Text = "1.0.11"
            };

            var releaseUpdateBtn = new Button
            {
                Text = "Release Update (force=false → force=true)",
                Location = new Point(40, 230),
                Size = new Size(300, 45),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(200, 150, 0),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            releaseUpdateBtn.FlatAppearance.BorderSize = 0;

            forceUpdateBtn.Click += (s, e) =>
            {
                string newVersion = bumpBox.Text.Trim();
                if (string.IsNullOrEmpty(newVersion))
                {
                    statusLabel.ForeColor = Color.Red;
                    statusLabel.Text = "Enter a version number";
                    return;
                }

                statusLabel.ForeColor = accentColor;
                statusLabel.Text = "Pushing version " + newVersion + " (force=true)...";
                content.Refresh();

                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "ENI-App");
                        client.DefaultRequestHeaders.Add("Authorization", "token " + loginToken);

                        string versionUrl = "https://api.github.com/repos/" + Program.GitHubUser + "/" + Program.GitHubRepo + "/contents/version.txt";
                        string getResp = client.GetStringAsync(versionUrl).GetAwaiter().GetResult();
                        dynamic getVersionJson = Program.ParseJson(getResp);
                        string currentSha = ((Dictionary<string,object>)getVersionJson)["sha"].ToString();

                        string encodedVersion = Convert.ToBase64String(Encoding.UTF8.GetBytes(newVersion + "|true\r\n"));

                        string updateBody = "{\"message\":\"Force update to v" + newVersion + "\",\"content\":\"" + encodedVersion + "\",\"sha\":\"" + currentSha + "\"}";

                        var content2 = new StringContent(updateBody, Encoding.UTF8, "application/json");
                        HttpResponseMessage putResp = client.PutAsync(versionUrl, content2).GetAwaiter().GetResult();

                        if (putResp.IsSuccessStatusCode)
                        {
                            statusLabel.ForeColor = Color.FromArgb(0, 255, 100);
                            statusLabel.Text = "Version bumped to " + newVersion + " (force=true)!\r\nAll users will auto-update on next launch.";
                        }
                        else
                        {
                            statusLabel.ForeColor = Color.Red;
                            statusLabel.Text = "Failed: " + putResp.StatusCode;
                        }
                    }
                }
                catch (Exception ex)
                {
                    statusLabel.ForeColor = Color.Red;
                    statusLabel.Text = "Error: " + ex.Message;
                }
            };

            releaseUpdateBtn.Click += (s, e) =>
            {
                statusLabel.ForeColor = accentColor;
                statusLabel.Text = "Fetching current version...";
                content.Refresh();

                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "ENI-App");
                        client.DefaultRequestHeaders.Add("Authorization", "token " + loginToken);

                        string versionUrl = "https://api.github.com/repos/" + Program.GitHubUser + "/" + Program.GitHubRepo + "/contents/version.txt";
                        string getResp = client.GetStringAsync(versionUrl).GetAwaiter().GetResult();
                        dynamic getVersionJson = Program.ParseJson(getResp);
                        string currentSha = ((Dictionary<string,object>)getVersionJson)["sha"].ToString();
                        string verContent = Encoding.UTF8.GetString(Convert.FromBase64String(getVersionJson["content"].ToString().Replace("\n", ""))).Trim();

                        string[] parts = verContent.Split('|');
                        string currentVersion = parts[0].Trim();
                        bool isForced = parts.Length > 1 && parts[1].Trim().ToLower() == "true";

                        if (isForced)
                        {
                            statusLabel.ForeColor = Color.FromArgb(200, 200, 0);
                            statusLabel.Text = "Already force=true for v" + currentVersion;
                            return;
                        }

                        string encodedVersion = Convert.ToBase64String(Encoding.UTF8.GetBytes(currentVersion + "|true\r\n"));

                        string verUpdateBody = "{\"message\":\"Release update v" + currentVersion + " (force=true)\",\"content\":\"" + encodedVersion + "\",\"sha\":\"" + currentSha + "\"}";

                        var verContent2 = new StringContent(verUpdateBody, Encoding.UTF8, "application/json");
                        HttpResponseMessage putResp = client.PutAsync(versionUrl, verContent2).GetAwaiter().GetResult();

                        if (putResp.IsSuccessStatusCode)
                        {
                            statusLabel.ForeColor = Color.FromArgb(0, 255, 100);
                            statusLabel.Text = "Released v" + currentVersion + " (force=true)!\r\nAll users will auto-update on next launch.";
                        }
                        else
                        {
                            statusLabel.ForeColor = Color.Red;
                            statusLabel.Text = "Failed: " + putResp.StatusCode;
                        }
                    }
                }
                catch (Exception ex)
                {
                    statusLabel.ForeColor = Color.Red;
                    statusLabel.Text = "Error: " + ex.Message;
                }
};

content.Controls.AddRange(new Control[] { title, info, forceUpdateBtn, bumpLabel, bumpBox, releaseUpdateBtn, statusLabel });
        }

        private void ShowSettings()
        {
            var title = MakeLabel("Settings", 24, FontStyle.Bold, Color.White, 40, 30);
            var versionInfo = MakeLabel("App Version: " + Program.CurrentVersion, 11, FontStyle.Regular, Color.FromArgb(120, 120, 130), 40, 80);
            var repoInfo = MakeLabel("Repo: github.com/drk6/.exe-app", 11, FontStyle.Regular, Color.FromArgb(120, 120, 130), 40, 110);
            var hotkeyInfo = MakeLabel("Circle hotkey: " + (circleHotkey != 0 ? ((Keys)circleHotkey).ToString() : "None") + "  |  Square hotkey: " + (squareHotkey != 0 ? ((Keys)squareHotkey).ToString() : "None"), 11, FontStyle.Regular, accentColor, 40, 160);
            content.Controls.AddRange(new Control[] { title, versionInfo, repoInfo, hotkeyInfo });
        }

        private Label MakeLabel(string text, float size, FontStyle style, Color color, int x, int y)
        {
            return new Label { Text = text, Font = new Font("Segoe UI", size, style), ForeColor = color, Location = new Point(x, y), AutoSize = true, BackColor = Color.Transparent };
        }

        public static object ParseJson(string json)
        {
            return new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(json);
        }
    }
}
































