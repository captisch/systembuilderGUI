using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using systembuilderGUI.ViewModels;

namespace systembuilderGUI.Models;

public class SystemBuilder
{
    public async Task call(string? pathToConfig, string? pathToDir, string? logFilePath)
    {
        if (string.IsNullOrWhiteSpace(pathToConfig)) return;

        var psi = new ProcessStartInfo()
        {
            FileName = "docker",                                                        // Docker executable
            Arguments = "run " +                                                        // run docker container in non-interactive mode
                        "--rm " +                                                       // and remove it after execution
                        $"-v \"{pathToConfig}:/systembuilder/configFile_demo_soc.yaml\" " + // mount config file to docker container
                        $"-v \"{logFilePath}:/systembuilder/log.txt\" " +
                        $"-v \"{pathToDir}:/systembuilder/build\" " +                       // mount build directory to docker container
                        "liteximg:latest " +                                            // used docker image
                        "sh -c " +                                                      // run command in shell
                        "\". .venv/bin/activate && " +                                  // activate virtual environment
                        "python3 litex_generator.py 2>&1 | tee log.txt\"" +                                // run LiteX generator script
                        "\n",                                                           // end of command
            UseShellExecute = true,
            CreateNoWindow = false
        };

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException("The docker process could not be started.");
        await process.WaitForExitAsync();
        await Task.Delay(100);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Docker exited with code {process.ExitCode}. See the container log: {logFilePath}");
    }
}