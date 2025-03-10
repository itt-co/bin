using System;
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
                //Console.WriteLine("[i] Usage: itt install <package-name>");
                return;
            }

            switch (args[0].ToLower())
            {
                case "i":
                    await HandleInstallCommand(args);
                    break;

                case "t":
                    await HandleTweakCommand(args);
                    break;
                default:
                    Console.WriteLine("[x] Unknown command. Use 'itt help'.");
                    break;
            }
        }

        static async Task HandleInstallCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("[i] Usage: itt i <package-name>");
                return;
            }

            var packageNames = args.Skip(1).Where(arg => arg != "-y").ToList();
            if (!packageNames.Any())
            {
                Console.WriteLine("[i] This package not exist on ITT");
                return;
            }

            bool autoConfirm = args.Contains("-y");

            foreach (var packageName in packageNames)
            {
                await InstallPackageAsync(packageName, autoConfirm);
            }
        }

        static async Task HandleTweakCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("[i] Usage: itt t <twaek name>");
                return;
            }

            var tewakNames = args.Skip(1).Where(arg => arg != "-y").ToList();
            if (!tewakNames.Any())
            {
                Console.WriteLine("[i] This tweak not exist on ITT");
                return;
            }

            bool autoConfirm = args.Contains("-y");

            foreach (var tweakName in tewakNames)
            {
                await InstallTewakAsync(tweakName, autoConfirm);
            }
        }

        static async Task InstallPackageAsync(string packageName, bool autoConfirm)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[i] By installing, you accept licenses for the packages.");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"[+] Installing the following package: {packageName}");
            Console.ResetColor();

            if (!autoConfirm)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Are you sure you want to install the package '{packageName}'? (yes/y to confirm)");
                string confirmation = Console.ReadLine()?.ToLower();
                if (confirmation != "yes" && confirmation != "y")
                {
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

        static async Task InstallTewakAsync(string tweakName, bool autoConfirm)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"[+] Applying the following twaeks: {tweakName}");
            Console.ResetColor();

            if (!autoConfirm)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Are you sure you want to applying '{tweakName}'? (yes/y to confirm)");
                string confirmation = Console.ReadLine()?.ToLower();
                if (confirmation != "yes" && confirmation != "y")
                {
                    return;
                }
            }

            await ExecuteRemoteScript(tweakName);
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
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[x] The remote name could not be resolved: 'raw.githubusercontent.com");
                Console.ResetColor();
                return null;
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
            catch 
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[x] Error writing install");
                Console.ResetColor();
            }
        }

        static async Task ExecuteInstallScriptInSameSession(string ittPath, string packageName)
        {
            string helperScriptPath = Path.Combine(ittPath, "automation", "functions", "Install-ITTPackage.ps1");
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

        static async Task ExecuteRemoteScript(string TwaekName)
        {
            string scriptUrl = $"https://raw.githubusercontent.com/itt-co/itt-tweaks/main/{TwaekName}/run.ps1";

            // Run PowerShell in the same session using PowerShell APIs
            using (PowerShell ps = PowerShell.Create())
            {
                string psScript = $"irm '{scriptUrl}' | iex";

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
    }
}
