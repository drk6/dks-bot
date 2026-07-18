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
        public static string CurrentVersion = "1.0.0";

        [STAThread]
        static void Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

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

                    string versionUrl = "https://api.github.com/repos/" + GitHubUser + "/" + GitHubRepo + "/contents/version.txt";
                    string versionResp = client.GetStringAsync(versionUrl).GetAwaiter().GetResult();
                    dynamic versionJson = ParseJson(versionResp);
                    string latestVersion = Encoding.UTF8.GetString(Convert.FromBase64String(versionJson.content.ToString().Replace("\n", ""))).Trim();

                    if (latestVersion != CurrentVersion)
                    {
                        DialogResult result = MessageBox.Show(
                            "New version available: v" + latestVersion + "\n\nUpdate now?",
                            "ENI Updater",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information
                        );

                        if (result == DialogResult.Yes)
                        {
                            DownloadUpdate();
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public static void DownloadUpdate()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "ENI-App");
                    client.DefaultRequestHeaders.Add("Authorization", "token " + GitHubToken);

                    string exeUrl = "https://api.github.com/repos/" + GitHubUser + "/" + GitHubRepo + "/contents/app.exe";
                    string exeResp = client.GetStringAsync(exeUrl).GetAwaiter().GetResult();
                    dynamic exeJson = ParseJson(exeResp);
                    byte[] exeBytes = Convert.FromBase64String(exeJson.content.ToString().Replace("\n", ""));

                    string currentPath = Assembly.GetExecutingAssembly().Location;
                    string tempPath = currentPath + ".new";
                    string dir = Path.GetDirectoryName(currentPath);
                    string batPath = Path.Combine(dir, "update.bat");

                    File.WriteAllBytes(tempPath, exeBytes);

                    string bat = "@echo off\r\n" +
                        "timeout /t 1 /nobreak >nul\r\n" +
                        "del /f /q \"" + currentPath + "\"\r\n" +
                        "rename \"" + tempPath + "\" \"" + Path.GetFileName(currentPath) + "\"\r\n" +
                        "del /f /q \"" + batPath + "\"\r\n" +
                        "start \"\" \"" + currentPath + "\"\r\n";

                    File.WriteAllText(batPath, bat);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = batPath,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    });

                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Update failed: " + ex.Message, "ENI Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

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
                        string url = "https://api.github.com/repos/" + Program.GitHubUser + "/" + Program.GitHubRepo + "/contents/version.txt";
                        string resp = client.GetStringAsync(url).GetAwaiter().GetResult();
                        dynamic json = Program.ParseJson(resp);
                        string latest = Encoding.UTF8.GetString(Convert.FromBase64String(json.content.ToString().Replace("\n", ""))).Trim();
                        if (latest != Program.CurrentVersion)
                        {
                            DialogResult r = MessageBox.Show("New version v" + latest + " available. Update now?", "Auto Update", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                            if (r == DialogResult.Yes)
                            {
                                try { this.Invoke(new Action(() => { Program.DownloadUpdate(); })); } catch { }
                            }
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
                    string user = json.login.ToString();
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

        private void HighlightButton(Button btn)
        {
            if (activeButton != null)
            {
                activeButton.ForeColor = Color.FromArgb(160, 160, 170);
                activeButton.BackColor = sidebarColor;
            }
            activeButton = btn;
            activeButton.ForeColor = Color.White;
            activeIndicator.Top = btn.Top + 1;
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
                    if (t == page || (page == "home" && b.Text == "Home") || (page == "dev" && b.Text == "Dev") || (page == "login" && b.Text == "Login"))
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
                var info2 = MakeLabel("No tokens needed - just click and authorize", 11, FontStyle.Regular, Color.FromArgb(80, 80, 90), 40, 105);

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
                    statusLabel.Text = "Opening browser for GitHub login...";
                    content.Refresh();

                    Thread loginThread = new Thread(() =>
                    {
                        try
                        {
                            string ghPath = "C:\\Program Files\\GitHub CLI\\gh.exe";

                            Process ghProcess = new Process();
                            ghProcess.StartInfo.FileName = "cmd.exe";
                            ghProcess.StartInfo.Arguments = "/c \"" + ghPath + "\" auth login --hostname github.com --git-protocol https --web";
                            ghProcess.StartInfo.UseShellExecute = true;
                            ghProcess.Start();
                            ghProcess.WaitForExit(120000);

                            if (ghProcess.ExitCode == 0)
                            {
                                System.Threading.Thread.Sleep(2000);

                                Process tokenProcess = new Process();
                                tokenProcess.StartInfo.FileName = "cmd.exe";
                                tokenProcess.StartInfo.Arguments = "/c \"" + ghPath + "\" auth token --hostname github.com";
                                tokenProcess.StartInfo.UseShellExecute = false;
                                tokenProcess.StartInfo.RedirectStandardOutput = true;
                                tokenProcess.StartInfo.CreateNoWindow = true;
                                tokenProcess.Start();
                                string token = tokenProcess.StandardOutput.ReadToEnd().Trim();
                                tokenProcess.WaitForExit();

                                if (!string.IsNullOrEmpty(token) && token.StartsWith("ghp_"))
                                {
                                    loginToken = token;

                                    using (HttpClient client = new HttpClient())
                                    {
                                        client.DefaultRequestHeaders.Add("User-Agent", "ENI-App");
                                        client.DefaultRequestHeaders.Add("Authorization", "token " + loginToken);
                                        string resp = client.GetStringAsync("https://api.github.com/user").GetAwaiter().GetResult();
                                        dynamic json = Program.ParseJson(resp);
                                        string user = json.login.ToString();
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
                                else
                                {
                                    try { this.Invoke(new Action(() =>
                                    {
                                        statusLabel.ForeColor = Color.Red;
                                        statusLabel.Text = "Login cancelled or failed";
                                        loginBtn.Enabled = true;
                                    })); } catch { }
                                }
                            }
                            else
                            {
                                try { this.Invoke(new Action(() =>
                                {
                                    statusLabel.ForeColor = Color.Red;
                                    statusLabel.Text = "gh auth login failed. Is GitHub CLI installed?";
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
                Text = "1.0.1"
            };

            var statusLabel = MakeLabel("", 11, FontStyle.Regular, accentColor, 40, 240);
            statusLabel.Size = new Size(600, 100);

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
                statusLabel.Text = "Pushing version " + newVersion + "...";
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
                        string currentSha = getVersionJson.sha.ToString();

                        string encodedVersion = Convert.ToBase64String(Encoding.UTF8.GetBytes(newVersion + "\r\n"));

                        string updateBody = "{\"message\":\"Force update to v" + newVersion + "\",\"content\":\"" + encodedVersion + "\",\"sha\":\"" + currentSha + "\"}";

                        var content2 = new StringContent(updateBody, Encoding.UTF8, "application/json");
                        HttpResponseMessage putResp = client.PutAsync(versionUrl, content2).GetAwaiter().GetResult();

                        if (putResp.IsSuccessStatusCode)
                        {
                            statusLabel.ForeColor = Color.FromArgb(0, 255, 100);
                            statusLabel.Text = "Version bumped to " + newVersion + "!\r\nAll users will be prompted to update on next launch.";
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

            var uploadBtn = new Button
            {
                Text = "Upload app.exe from local",
                Location = new Point(40, 280),
                Size = new Size(300, 45),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 120, 200),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            uploadBtn.FlatAppearance.BorderSize = 0;

            uploadBtn.Click += (s, e) =>
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "Executable|*.exe";
                ofd.Title = "Select app.exe to upload";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    statusLabel.ForeColor = accentColor;
                    statusLabel.Text = "Uploading...";
                    content.Refresh();

                    try
                    {
                        using (HttpClient client = new HttpClient())
                        {
                            client.DefaultRequestHeaders.Add("User-Agent", "ENI-App");
                            client.DefaultRequestHeaders.Add("Authorization", "token " + loginToken);

                            string exeUrl = "https://api.github.com/repos/" + Program.GitHubUser + "/" + Program.GitHubRepo + "/contents/app.exe";
                            string getResp = client.GetStringAsync(exeUrl).GetAwaiter().GetResult();
                            dynamic getJson = Program.ParseJson(getResp);
                            string currentSha = getJson.sha.ToString();

                            byte[] exeBytes = File.ReadAllBytes(ofd.FileName);
                            string encodedExe = Convert.ToBase64String(exeBytes);

                            string updateBody = "{\"message\":\"Update app.exe\",\"content\":\"" + encodedExe + "\",\"sha\":\"" + currentSha + "\"}";

                            var content2 = new StringContent(updateBody, Encoding.UTF8, "application/json");
                            HttpResponseMessage putResp = client.PutAsync(exeUrl, content2).GetAwaiter().GetResult();

                            if (putResp.IsSuccessStatusCode)
                            {
                                statusLabel.ForeColor = Color.FromArgb(0, 255, 100);
                                statusLabel.Text = "app.exe uploaded!\r\nBump version to force users to update.";
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
                }
            };

            content.Controls.AddRange(new Control[] { title, info, forceUpdateBtn, bumpLabel, bumpBox, statusLabel, uploadBtn });
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
    }
}
