using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ENIApp
{
    class Program
    {
        static string GitHubUser = "drk6";
        static string GitHubRepo = ".exe-app";
        static string GitHubToken = "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";
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

        static bool CheckForUpdate()
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

        static void DownloadUpdate()
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

        static object ParseJson(string json)
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

        public MainForm()
        {
            Text = "ENI App";
            Size = new Size(950, 650);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = bgColor;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 10);

            sidebar = new Panel
            {
                Width = 220,
                Dock = DockStyle.Left,
                BackColor = sidebarColor,
                Padding = new Padding(0, 15, 0, 0)
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
            Button btnSettings = CreateSidebarButton("Settings", 520);

            btnHome.Click += (s, e) => ShowPage("home");
            btnCircle.Click += (s, e) => ShowPage("circle");
            btnSquare.Click += (s, e) => ShowPage("square");
            btnSettings.Click += (s, e) => ShowPage("settings");

            sidebar.Controls.AddRange(new Control[] { titleLabel, versionLabel, activeIndicator, btnHome, btnCircle, btnSquare, btnSettings });

            content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = bgColor,
                Margin = new Padding(220, 0, 0, 0),
                AutoScroll = true
            };

            Controls.Add(content);
            Controls.Add(sidebar);

            ShowPage("home");
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
                    if (b.Text.ToLower().Contains(page) || (page == "home" && b.Text == "Home"))
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
            }
        }

        private void ShowHome()
        {
            var title = MakeLabel("Welcome to ENI App", 24, FontStyle.Bold, Color.White, 40, 30);
            var sub = MakeLabel("Your all-in-one tool hub", 12, FontStyle.Regular, Color.FromArgb(100, 100, 110), 40, 75);

            var circleCard = MakeCard("Circle Draw", "Draw a perfect circle on your desktop", 40, 130, 280, 120);
            circleCard.Click += (s, e) => ShowPage("circle");

            var squareCard = MakeCard("Square Draw", "Draw a perfect square on your desktop", 340, 130, 280, 120);
            squareCard.Click += (s, e) => ShowPage("square");

            content.Controls.AddRange(new Control[] { title, sub, circleCard, squareCard });
        }

        private Panel MakeCard(string title, string desc, int x, int y, int w, int h)
        {
            var card = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = cardColor
            };

            var t = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = accentColor,
                Location = new Point(15, 15),
                AutoSize = true
            };

            var d = new Label
            {
                Text = desc,
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(120, 120, 130),
                Location = new Point(15, 50),
                Size = new Size(w - 30, 40)
            };

            card.Controls.AddRange(new Control[] { t, d });
            card.Cursor = Cursors.Hand;

            card.MouseEnter += (s, e) => card.BackColor = hoverColor;
            card.MouseLeave += (s, e) => card.BackColor = cardColor;

            return card;
        }

        private void ShowCircle()
        {
            var title = MakeLabel("Circle Draw", 24, FontStyle.Bold, Color.White, 40, 30);
            var sub = MakeLabel("Position your cursor on the circle outline, then press Start", 11, FontStyle.Regular, Color.FromArgb(100, 100, 110), 40, 75);

            var radiusLabel = MakeLabel("Radius (px):", 11, FontStyle.Regular, Color.FromArgb(160, 160, 170), 40, 120);
            var radiusBox = new TextBox
            {
                Location = new Point(160, 117),
                Size = new Size(100, 30),
                BackColor = cardColor,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11),
                Text = "100"
            };

            var statusLabel = MakeLabel("", 11, FontStyle.Regular, accentColor, 40, 200);
            statusLabel.Size = new Size(600, 30);

            var startBtn = new Button
            {
                Text = "Start Drawing",
                Location = new Point(40, 165),
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
                int radius = 100;
                int.TryParse(radiusBox.Text, out radius);
                statusLabel.ForeColor = accentColor;
                statusLabel.Text = "Get ready... move cursor to circle position!";
                content.Refresh();
                System.Threading.Thread.Sleep(1500);

                POINT pos;
                GetCursorPos(out pos);
                double cx = pos.X;
                double cy = pos.Y;

                statusLabel.Text = "Drawing circle...";
                content.Refresh();

                int steps = 800;
                SetCursorPos((int)(cx + radius), (int)cy);
                System.Threading.Thread.Sleep(100);
                mouse_event(0x02, 0, 0, 0, IntPtr.Zero);

                var sw = Stopwatch.StartNew();
                for (int i = 1; i <= steps; i++)
                {
                    double angle = (2.0 * Math.PI * i) / steps;
                    int x = (int)Math.Round(cx + (radius * Math.Cos(angle)));
                    int y = (int)Math.Round(cy + (radius * Math.Sin(angle)));
                    SetCursorPos(x, y);
                    double expected = i * 6.0;
                    while (sw.Elapsed.TotalMilliseconds < expected) { }
                }

                SetCursorPos((int)(cx + radius), (int)cy);
                mouse_event(0x04, 0, 0, 0, IntPtr.Zero);

                statusLabel.ForeColor = Color.FromArgb(0, 255, 100);
                statusLabel.Text = "Circle drawn!";
            };

            content.Controls.AddRange(new Control[] { title, sub, radiusLabel, radiusBox, startBtn, statusLabel });
        }

        private void ShowSquare()
        {
            var title = MakeLabel("Square Draw", 24, FontStyle.Bold, Color.White, 40, 30);
            var sub = MakeLabel("Position your cursor where you want the top-left corner, then press Start", 11, FontStyle.Regular, Color.FromArgb(100, 100, 110), 40, 75);

            var sizeLabel = MakeLabel("Size (px):", 11, FontStyle.Regular, Color.FromArgb(160, 160, 170), 40, 120);
            var sizeBox = new TextBox
            {
                Location = new Point(160, 117),
                Size = new Size(100, 30),
                BackColor = cardColor,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11),
                Text = "300"
            };

            var statusLabel = MakeLabel("", 11, FontStyle.Regular, accentColor, 40, 200);
            statusLabel.Size = new Size(600, 30);

            var startBtn = new Button
            {
                Text = "Start Drawing",
                Location = new Point(40, 165),
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
                int size = 300;
                int.TryParse(sizeBox.Text, out size);
                statusLabel.ForeColor = accentColor;
                statusLabel.Text = "Get ready... move cursor to top-left corner!";
                content.Refresh();
                System.Threading.Thread.Sleep(1500);

                POINT pos;
                GetCursorPos(out pos);
                double sx = pos.X;
                double sy = pos.Y;

                statusLabel.Text = "Drawing square...";
                content.Refresh();

                int stepsPerSide = 200;
                SetCursorPos((int)sx, (int)sy);
                System.Threading.Thread.Sleep(100);
                mouse_event(0x02, 0, 0, 0, IntPtr.Zero);

                var sw = Stopwatch.StartNew();
                double targetMs = 3.0;

                for (int i = 0; i <= stepsPerSide; i++)
                {
                    double t = (double)i / stepsPerSide;
                    SetCursorPos((int)(sx + size * t), (int)sy);
                    double expected = i * targetMs;
                    while (sw.Elapsed.TotalMilliseconds < expected) { }
                }
                for (int i = 0; i <= stepsPerSide; i++)
                {
                    double t = (double)i / stepsPerSide;
                    SetCursorPos((int)(sx + size), (int)(sy + size * t));
                    double expected = (stepsPerSide + i) * targetMs;
                    while (sw.Elapsed.TotalMilliseconds < expected) { }
                }
                for (int i = 0; i <= stepsPerSide; i++)
                {
                    double t = (double)i / stepsPerSide;
                    SetCursorPos((int)(sx + size - size * t), (int)(sy + size));
                    double expected = (stepsPerSide * 2 + i) * targetMs;
                    while (sw.Elapsed.TotalMilliseconds < expected) { }
                }
                for (int i = 0; i <= stepsPerSide; i++)
                {
                    double t = (double)i / stepsPerSide;
                    SetCursorPos((int)sx, (int)(sy + size - size * t));
                    double expected = (stepsPerSide * 3 + i) * targetMs;
                    while (sw.Elapsed.TotalMilliseconds < expected) { }
                }

                mouse_event(0x04, 0, 0, 0, IntPtr.Zero);

                statusLabel.ForeColor = Color.FromArgb(0, 255, 100);
                statusLabel.Text = "Square drawn!";
            };

            content.Controls.AddRange(new Control[] { title, sub, sizeLabel, sizeBox, startBtn, statusLabel });
        }

        private void ShowSettings()
        {
            var title = MakeLabel("Settings", 24, FontStyle.Bold, Color.White, 40, 30);
            var versionInfo = MakeLabel("App Version: " + Program.CurrentVersion, 11, FontStyle.Regular, Color.FromArgb(120, 120, 130), 40, 80);
            var repoInfo = MakeLabel("Repo: github.com/drk6/.exe-app", 11, FontStyle.Regular, Color.FromArgb(120, 120, 130), 40, 110);

            content.Controls.AddRange(new Control[] { title, versionInfo, repoInfo });
        }

        private Label MakeLabel(string text, float size, FontStyle style, Color color, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", size, style),
                ForeColor = color,
                Location = new Point(x, y),
                AutoSize = true
            };
        }
    }
}
