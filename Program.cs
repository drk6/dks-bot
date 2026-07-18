using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
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
        private Label titleLabel;
        private Label versionLabel;
        private Button btnHome;
        private Button btnScripts;
        private Button btnSettings;

        public MainForm()
        {
            Text = "ENI App";
            Size = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 30);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            sidebar = new Panel
            {
                Size = new Size(200, 600),
                BackColor = Color.FromArgb(20, 20, 20),
                Dock = DockStyle.Left
            };

            titleLabel = new Label
            {
                Text = "ENI",
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 200, 255),
                Location = new Point(20, 20),
                AutoSize = true
            };

            versionLabel = new Label
            {
                Text = "v" + Program.CurrentVersion,
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.Gray,
                Location = new Point(20, 60),
                AutoSize = true
            };

            btnHome = CreateButton("Home", 100);
            btnScripts = CreateButton("Scripts", 150);
            btnSettings = CreateButton("Settings", 200);

            btnHome.Click += (s, e) => ShowPage("home");
            btnScripts.Click += (s, e) => ShowPage("scripts");
            btnSettings.Click += (s, e) => ShowPage("settings");

            sidebar.Controls.AddRange(new Control[] { titleLabel, versionLabel, btnHome, btnScripts, btnSettings });

            content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(10, 10, 10, 10)
            };

            Controls.Add(content);
            Controls.Add(sidebar);
            sidebar.BringToFront();

            ShowPage("home");
        }

        private Button CreateButton(string text, int y)
        {
            return new Button
            {
                Text = text,
                Size = new Size(200, 40),
                Location = new Point(0, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0),
                Cursor = Cursors.Hand
            };
        }

        private void ShowPage(string page)
        {
            content.Controls.Clear();

            var titleLabel2 = new Label
            {
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(30, 30),
                AutoSize = true
            };

            var descLabel = new Label
            {
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.Gray,
                Location = new Point(30, 80),
                Size = new Size(600, 40)
            };

            switch (page)
            {
                case "home":
                    titleLabel2.Text = "Welcome to ENI App";
                    descLabel.Text = "Your all-in-one tool hub. Use the sidebar to navigate.";
                    break;
                case "scripts":
                    titleLabel2.Text = "Scripts";
                    descLabel.Text = "Your scripts and tools will appear here.";
                    break;
                case "settings":
                    titleLabel2.Text = "Settings";
                    descLabel.Text = "App settings and configuration.";
                    break;
            }

            content.Controls.AddRange(new Control[] { titleLabel2, descLabel });
        }
    }
}
