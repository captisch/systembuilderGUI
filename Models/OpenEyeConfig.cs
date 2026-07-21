using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;

namespace systembuilderGUI.Models;

public partial class OpenEyeConfig : ObservableObject
{
    public OpenEyeConfig()
    {
        Parameters = ReadParametersFromTemplate(new Uri("avares://systembuilderGUI/Assets/openEyeParameterTemplate.vh"));
    }

    public OpenEyeConfig(ObservableCollection<Parameter> parameters)
    {
        Parameters = parameters;
    }
    
    public ObservableCollection<Parameter> Parameters { get; set; }

    private ObservableCollection<Parameter> ReadParametersFromTemplate(Uri templateFile)
    {
        using var stream = AssetLoader.Open(templateFile);
        using var reader = new StreamReader(stream);
        var headerText = reader.ReadToEnd();
        return ParseParameters(headerText);
    }
    
    private ObservableCollection<Parameter> ParseParameters(string? fileText)
    {
        var parameters = new ObservableCollection<Parameter>();
        if (string.IsNullOrWhiteSpace(fileText))
        {
            Debug.WriteLine("The requested header file does not exist or is invalid!");
            return parameters;
        }
        
        var regexPatternParameterlist = @"(?:parameter(?<parameterlist>\s*\S*\s*=\s*\S*))";
        
        var matches = Regex.Matches(fileText, regexPatternParameterlist);
        Debug.WriteLine($"Found {matches.Count} matches");
        //Debug.WriteLine("In Text:" + headerText);
        
        foreach (Match match in matches)
        {
            var parameterlist = match.Groups["parameterlist"].Value;
            Debug.WriteLine("Using parameterlist: "  + parameterlist);
            var parameterText = parameterlist.Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p));
            
            foreach (var parameter in parameterText)
            {
                var parameterName = parameter.Split('=')[0].Trim();
                var parameterValue = parameter.Split('=')[1].Trim();
                
                Debug.WriteLine("Found Parameter: "+ parameterName);
                
                parameters.Add(new Parameter 
                {
                    Name = parameterName, 
                    Value = parameterValue
                });
            }
        }
        Debug.WriteLine("If there are no parameters listed before this message, something went wrong.");
        return parameters;
    }

    public async Task LoadParametersFromFileAsync()
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var window = lifetime?.MainWindow;
        if (window?.StorageProvider is null)
            return;
        FilePickerFileType[] filetypes = [new FilePickerFileType("Verilog Header") { Patterns = ["*.vh"] }
        ];
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose OpenEye Header File",
            AllowMultiple = false,
            FileTypeFilter = filetypes
        });

        if (files.Count != 1)
        {
            Debug.WriteLine("Only one header file can be chosen. You shouldn't be able to get here.");
        }
        else
        {
            var filepath = files[0].TryGetLocalPath();
            var fileText = await File.ReadAllTextAsync(filepath);
            Parameters = ParseParameters(fileText);
            
            //Temporary Code for debugging purposes
            Console.WriteLine("The following parameters were read from the file:");
            foreach (var parameter in Parameters)
            {
                Console.WriteLine(parameter.Name + " = " + parameter.Value);
            }
        }
    }

    public async Task GenerateHeaderAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("No path was given to save the header file.");
        
        //string outputPath = Path.Combine(targetDirectory, "parameters.vh");

        await using StreamWriter sw = File.CreateText(path);
        await sw.WriteLineAsync("//This header has been created automatically");
        await sw.WriteLineAsync("");
        foreach (var parameter in Parameters)
        {
            await sw.WriteLineAsync("parameter " + parameter.Name + "=" + parameter.Value + ",");    
        }
    }

    /* Derzeit auf Eis wegen konzeptioneller Probleme!
    public async Task OpenEyeCall(string? pathToDir)
    {
        if (string.IsNullOrWhiteSpace(pathToDir)) return;

        var psi = new ProcessStartInfo()
        {
            FileName = "docker",                                                        // Docker executable
            Arguments = "run " +                                                        // run docker container in non-interactive mode
                        "--rm " +                                                       // and remove it after execution
                        $"-v \"{pathToDir}:/systembuilder/build\" " +                   // mount directory to docker container
                        "liteximg:220626 " +                                            // used docker image
                        "sh -c " +                                                      // run command in shell
                        "\". .venv/bin/activate && " +                                  // activate virtual environment
                        "python3 litex_generator.py 2>&1 | tee log.txt\"" +                                // run LiteX generator script
                        "\n",                                                           // end of command
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
    }*/
}