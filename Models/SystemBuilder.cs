using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using systembuilderGUI.ViewModels;

namespace systembuilderGUI.Models;

public class SystemBuilder
{
    public async Task call(string? pathToConfig, string? pathToDir)
    {
        if (string.IsNullOrWhiteSpace(pathToConfig)) return;

        var psi = new ProcessStartInfo()
        {
            FileName = "docker",                                                // Docker executable
            Arguments = "run --rm " +                                           // run docker container in non-interactive mode and remove it after execution
                        $"-v {pathToConfig}:/litex/configFile_demo_soc.yaml " + // mount config file to docker container
                        $"-v {pathToDir}:/litex/build " +                       // mount build directory to docker container
                        "liteximg:latest " +                                    // used docker image
                        "sh -c " +                                              // run command in shell
                        "\". venv/bin/activate && " +                           // activate virtual environment
                        "python3 litex_generator.py\"" +                        // run LiteX generator script
                        "\n",                                                   // end of command
            UseShellExecute = true,
            CreateNoWindow = false
        };

        try
        {
            using (var process = Process.Start(psi))
            {
                if (process is null) return;
                process.WaitForExit();
                await Task.Delay(100);
                //return process.ExitCode;
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
    }
}