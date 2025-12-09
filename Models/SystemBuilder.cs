using System;
using System.Diagnostics;
using System.IO;
using systembuilderGUI.ViewModels;

namespace systembuilderGUI.Models;

public class SystemBuilder
{
    public int call(string? pathToConfig)
    {
        var wslPath = WSL.BuildWslPath(pathToConfig);
        
        var psi = new ProcessStartInfo()
        {
            // wsl directory structure
            // ~/  (home directory)
            // |-- liteX/
            // |   |-- SystemBuilder/
            // |   |   |-- LiteX-related/
            // |   |   |   |-- Python/
            // |   |   |   |   |-- litex_generator.py
            // |   |  
            // |   |-- everthing litex related
            // |   |-- venv
            
            FileName = "wsl.exe",
            Arguments = "cd ~/liteX\n" +                                // Path to LiteX directory
                        $"cp {wslPath} configFile_output.yaml\n" +      // Copy config file to LiteX directory
                        "source venv/bin/activate\n" +                  // Activate virtual environment
                        "python3 SystemBuilder/LiteX-related/Python/litex_generator.py\n" + // Run LiteX generator
                        "\nread -p \"Press enter to continue...\" x",   // Wait for user input
            UseShellExecute = true,
            CreateNoWindow = false
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return -1;
            process.WaitForExit();
            return process.ExitCode;
        }
        catch ( Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
        return 0;
    }
    
}