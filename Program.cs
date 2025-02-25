using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Management.Automation;

namespace ITT
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();

        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("[i] Usage: itt install <package-name>");
                return;
            }

            switch (args[0].ToLower())
            {
                case "install":
                    await HandleInstallCommand(args);
                    break;
                default:
                    Console.WriteLine("[x] Unknown command. Use 'itt install <package-name>'.");
                    break;
            }
        }

        static async Task HandleInstallCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("[i] Usage: itt install <package-name>");
                return;
            }

            var packageNames = args.Skip(1).Where(arg => arg != "-y").ToList();
            if (!packageNames.Any())
            {
                Console.WriteLine("No package names provided.");
                return;
            }

            bool autoConfirm = args.Contains("-y");

            foreach (var packageName in packageNames)
            {
                await InstallPackageAsync(packageName, autoConfirm);
            }
        }

        static async Task InstallPackageAsync(string packageName, bool autoConfirm)
        {
            if (!await PackageExistsOnGitHub(packageName))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[i] Package ({packageName}) does not exist in package list");
                Console.WriteLine($"[i] Use <itt search> to see all available packages");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[i] By installing, you accept licenses for the packages.");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[+] Installing the following package: {packageName}");
            Console.ResetColor();

            if (!autoConfirm)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Are you sure you want to install the package '{packageName}'? (yes/y to confirm)");
                string confirmation = Console.ReadLine()?.ToLower();
                if (confirmation != "yes" && confirmation != "y")
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Installation canceled.");
                    Console.ResetColor();
                    return;
                }
            }

            string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string ittPath = Path.Combine(programDataPath, "itt");
            string libPath = Path.Combine(ittPath, "downloads");

            CreateDirectoriesIfNotExist(ittPath, libPath, packageName);

            string installScriptContent = await DownloadInstallScript(packageName);
            await WriteInstallScriptToFile(ittPath, packageName, installScriptContent);

            await ExecuteInstallScriptInSameSession(ittPath, packageName);
        }

        static void CreateDirectoriesIfNotExist(string ittPath, string libPath, string packageName)
        {
            if (!Directory.Exists(ittPath))
            {
                Directory.CreateDirectory(ittPath);
                Directory.CreateDirectory(libPath);
            }

            string packageFolder = Path.Combine(libPath, packageName);
            Directory.CreateDirectory(packageFolder);
            string toolsFolder = Path.Combine(packageFolder, "tools");
            Directory.CreateDirectory(toolsFolder);
        }

        static async Task<string> DownloadInstallScript(string packageName)
        {
            string packageUrl = $"https://raw.githubusercontent.com/itt-co/itt-packages/main/automation/{packageName}/install.ps1";
            try
            {
                return await client.GetStringAsync(packageUrl);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[x] Error downloading install script: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }

        static async Task WriteInstallScriptToFile(string ittPath, string packageName, string installScriptContent)
        {
            string toolsFolder = Path.Combine(ittPath, "downloads", packageName, "tools");
            string installScriptPath = Path.Combine(toolsFolder, "install.ps1");

            try
            {
                using (var writer = new StreamWriter(installScriptPath))
                {
                    await writer.WriteAsync(installScriptContent);
                }
       
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[x] Error writing install: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }

        static async Task ExecuteInstallScriptInSameSession(string ittPath, string packageName)
        {
            string helperScriptPath = Path.Combine(ittPath, "helpers", "functions", "Install-ITTPackage.ps1");
            string installScriptPath = Path.Combine(ittPath, "downloads", packageName, "tools", "install.ps1");

            string psScript = $@"
                $env:PSModulePath = ';{Path.GetDirectoryName(helperScriptPath)}'
                . '{helperScriptPath}'
                . '{installScriptPath}'
            ";

            // Run PowerShell in the same session using PowerShell APIs
            using (PowerShell ps = PowerShell.Create())
            {
                ps.AddScript(psScript);
                ps.Streams.Error.DataAdded += (sender, e) =>
                {
                    var error = ps.Streams.Error[e.Index];
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(error.ToString());
                    Console.ResetColor();
                };

                ps.Streams.Information.DataAdded += (sender, e) =>
                {
                    var info = ps.Streams.Information[e.Index].ToString();
                    Console.Write("\r" + info); 
                };

                ps.Invoke();
            }
        }

        static async Task<bool> PackageExistsOnGitHub(string packageName)
        {
            string apiUrl = $"https://raw.githubusercontent.com/itt-co/itt-packages/main/automation/{packageName}/install.ps1";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "ITT-Client");

                HttpResponseMessage response = await client.GetAsync(apiUrl);
                return response.IsSuccessStatusCode;
            }
        }
    }
}
