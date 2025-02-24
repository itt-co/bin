// Copyright (c) 2025 ITT Co. All rights reserved.
// Author: Emad Adel (ITT)
// This code is licensed under the Apache License 2.0.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ITT
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();

        static async Task Main(string[] args)
        {
            // Check if there are arguments passed in the command line
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: itt install <package-name>");
                return;
            }

            // Handle the command using switch
            switch (args[0].ToLower())
            {
                case "install":
                    await HandleInstallCommand(args);
                    break;
                default:
                    Console.WriteLine("Unknown command. Use 'itt install <package-name>'.");
                    break;
            }
        }

        static async Task HandleInstallCommand(string[] args)
        {
            // Check if there are any package names provided
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: itt install <package-name>");
                return;
            }

            // Filter out the "-y" argument and get the package names
            var packageNames = args.Skip(1).Where(arg => arg != "-y").ToList();

            // Check if there are any package names
            if (!packageNames.Any())
            {
                Console.WriteLine("No package names provided.");
                return;
            }

            bool autoConfirm = args.Contains("-y"); // Check if -y is present

            // Install each package
            foreach (var packageName in packageNames)
            {
                await InstallPackageAsync(packageName, autoConfirm);
            }
        }

        // Asynchronously install a package from GitHub
        static async Task InstallPackageAsync(string packageName, bool autoConfirm)
        {
            // Check if the package exists on GitHub
            if (!await PackageExistsOnGitHub(packageName))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Package ({packageName}) does not exist in package list");
                Console.WriteLine($"Use <itt search> to see all available packages");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("By installing, you accept licenses for the packages.");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Installing the following package: {packageName}");
            Console.ResetColor();

            // If not auto-confirmed, prompt user for confirmation
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

            // Define paths for the installation
            string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string ittPath = Path.Combine(programDataPath, "itt");
            string libPath = Path.Combine(ittPath, "downloads");

            // Create necessary directories if they don't exist
            CreateDirectoriesIfNotExist(ittPath, libPath, packageName);

            // Download and write the installation script
            string installScriptContent = await DownloadInstallScript(packageName);
            await WriteInstallScriptToFile(ittPath, packageName, installScriptContent);

            // Execute the install script using PowerShell
            await ExecuteInstallScript(ittPath, packageName);
        }

        // Create necessary directories for package installation
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

        // Download the install script content from GitHub
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
                Console.WriteLine($"Error downloading install script: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }

        // Write the install script content to a file
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
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Downloading package from source: https://github.com/itt-co/itt-packages");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error writing install: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }

        // Execute the install script using PowerShell
        static async Task ExecuteInstallScript(string ittPath, string packageName)
        {
            string helperScriptPath = Path.Combine(ittPath, "helpers", "functions", "Install-ITTPackage.ps1");
            string installScriptPath = Path.Combine(ittPath, "downloads", packageName, "tools", "install.ps1");

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-ExecutionPolicy Bypass -NoProfile -Command \". '{helperScriptPath}'; & '{installScriptPath}'\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            try
            {
                using (Process process = Process.Start(psi))
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    process.WaitForExit();

                    Console.WriteLine(output);
                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Error: " + error);
                        Console.ResetColor();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error executing install: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }

        // Check if a specific package exists on GitHub
        static async Task<bool> PackageExistsOnGitHub(string packageName)
        {
            string apiUrl = $"https://raw.githubusercontent.com/itt-co/itt-packages/main/automation/{packageName}/install.ps1";

            using (HttpClient client = new HttpClient())
            {
                // GitHub requires User-Agent header
                client.DefaultRequestHeaders.Add("User-Agent", "ITT-Client");

                HttpResponseMessage response = await client.GetAsync(apiUrl);
                return response.IsSuccessStatusCode;
            }
        }
    }
}