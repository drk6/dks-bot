using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace AutoUpdater
{
    class Program
    {
        static string GitHubUser = "drk6";
        static string GitHubRepo = ".exe-app";
        static string CurrentVersion = "1.0.0";

        static void Main(string[] args)
        {
            Run().GetAwaiter().GetResult();
        }

        static async Task Run()
        {
            Console.Title = "ENI Auto Updater";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================");
            Console.WriteLine("   ENI Auto Updater v" + CurrentVersion);
            Console.WriteLine("================================");
            Console.ResetColor();
            Console.WriteLine();

            bool shouldUpdate = false;

            try
            {
                Console.WriteLine("[*] Checking for updates...");
                string latestVersion = await GetLatestVersion();

                if (latestVersion == null)
                {
                    Console.WriteLine("[!] Could not check for updates. Continuing...");
                }
                else if (latestVersion == CurrentVersion)
                {
                    Console.WriteLine("[+] You are up to date! (v" + CurrentVersion + ")");
                }
                else
                {
                    Console.WriteLine("[!] New version available: v" + latestVersion);
                    Console.WriteLine("[*] Downloading update...");
                    shouldUpdate = await DownloadUpdate(latestVersion);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[!] Update check failed: " + ex.Message);
            }

            Console.WriteLine();
            Console.WriteLine("[*] Starting application...");
            Console.WriteLine();

            StartMainApp();

            if (shouldUpdate)
            {
                Console.WriteLine("[*] Update downloaded. Will apply on next restart.");
            }

            Console.WriteLine("Press any key to exit updater...");
            Console.ReadKey();
        }

        static async Task<string> GetLatestVersion()
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "ENI-Updater");

                string url = "https://raw.githubusercontent.com/" + GitHubUser + "/" + GitHubRepo + "/main/version.txt";
                string version = await client.GetStringAsync(url);
                return version.Trim();
            }
        }

        static async Task<bool> DownloadUpdate(string version)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "ENI-Updater");

                string exeUrl = "https://raw.githubusercontent.com/" + GitHubUser + "/" + GitHubRepo + "/main/app.exe";
                string currentPath = Assembly.GetExecutingAssembly().Location;
                string tempPath = currentPath + ".new";
                string dir = Path.GetDirectoryName(currentPath);
                string batPath = Path.Combine(dir, "update.bat");

                byte[] exeBytes = await client.GetByteArrayAsync(exeUrl);
                File.WriteAllBytes(tempPath, exeBytes);

                string batContent = "@echo off\r\n" +
                    "timeout /t 2 /nobreak >nul\r\n" +
                    "del /f /q \"" + currentPath + "\"\r\n" +
                    "rename \"" + tempPath + "\" \"" + Path.GetFileName(currentPath) + "\"\r\n" +
                    "del /f /q \"" + batPath + "\"\r\n" +
                    "start \"\" \"" + currentPath + "\"\r\n";

                File.WriteAllText(batPath, batContent);

                Process.Start(new ProcessStartInfo
                {
                    FileName = batPath,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });

                return true;
            }
        }

        static void StartMainApp()
        {
            string appPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "main_app.exe");

            if (File.Exists(appPath))
            {
                Process.Start(appPath);
            }
            else
            {
                Console.WriteLine("[!] main_app.exe not found.");
                Console.WriteLine("[*] Place your app as main_app.exe in the same folder.");
            }
        }
    }
}
