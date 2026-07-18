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
            Console.Title = "ENI App";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================");
            Console.WriteLine("         ENI App v" + CurrentVersion);
            Console.WriteLine("================================");
            Console.ResetColor();
            Console.WriteLine();

            try
            {
                Console.WriteLine("[*] Checking for updates...");
                string latestVersion = await GetLatestVersion();

                if (latestVersion != null && latestVersion != CurrentVersion)
                {
                    Console.WriteLine("[!] New version available: v" + latestVersion);
                    Console.WriteLine("[*] Downloading update...");
                    await DownloadAndRestart(latestVersion);
                    return;
                }
                else
                {
                    Console.WriteLine("[+] Up to date! (v" + CurrentVersion + ")");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[!] Update check failed: " + ex.Message);
            }

            Console.WriteLine();
            Console.WriteLine("[*] App is running! Do your stuff here.");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task<string> GetLatestVersion()
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "ENI-App");
                string url = "https://raw.githubusercontent.com/" + GitHubUser + "/" + GitHubRepo + "/main/version.txt";
                string version = await client.GetStringAsync(url);
                return version.Trim();
            }
        }

        static async Task DownloadAndRestart(string version)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "ENI-App");

                string exeUrl = "https://raw.githubusercontent.com/" + GitHubUser + "/" + GitHubRepo + "/main/app.exe";
                string currentPath = Assembly.GetExecutingAssembly().Location;
                string tempPath = currentPath + ".new";
                string dir = Path.GetDirectoryName(currentPath);
                string batPath = Path.Combine(dir, "update.bat");

                byte[] exeBytes = await client.GetByteArrayAsync(exeUrl);
                File.WriteAllBytes(tempPath, exeBytes);

                Console.WriteLine("[+] Update downloaded. Restarting...");

                string batContent = "@echo off\r\n" +
                    "timeout /t 1 /nobreak >nul\r\n" +
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

                Environment.Exit(0);
            }
        }
    }
}
