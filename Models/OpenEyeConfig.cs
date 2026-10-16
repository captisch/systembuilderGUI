using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using systembuilderGUI.Services;

namespace systembuilderGUI.Models;

public partial class OpenEyeParameter : Parameter
{
    [ObservableProperty] private string? description;
}

public class OpenEyeConfig : ObservableObject
{
    public OpenEyeConfig()
    {
        Parameters = ReadParametersFromTemplate(new Uri("avares://systembuilderGUI/Assets/openEyeParameterTemplate.txt"));
    }

    public OpenEyeConfig(ObservableCollection<OpenEyeParameter> parameters)
    {
        Parameters = parameters;
    }
    
    public ObservableCollection<OpenEyeParameter> Parameters { get; set; }

    private ObservableCollection<OpenEyeParameter> ReadParametersFromTemplate(Uri templateFile)
    {
        using var stream = AssetLoader.Open(templateFile);
        using var reader = new StreamReader(stream);
        var headerText = reader.ReadToEnd();
        return ParseParameters(headerText);
    }

    public ObservableCollection<OpenEyeParameter> ParseParameters(string? fileText)
    {
        if (fileText == null)
        {
            Debug.WriteLine("ParseParameters: fileText is null!");
            return [];
        }
        // Matches: parameter NAME = VALUE   (optional trailing comma, optional whitespace around '=')
        var parameterPattern = new Regex(@"^\s*parameter\s+(\S+)\s*=\s*(.+?)\s*,?\s*$", RegexOptions.Compiled);

        // Matches: description NAME = "TEXT"   (optional trailing comma, optional whitespace around '=')
        var descriptionPattern = new Regex(@"^\s*description\s+(\S+)\s*=\s*""(.*)""\s*,?\s*$", RegexOptions.Compiled);

        var byName = new Dictionary<string, OpenEyeParameter>();
        var result = new ObservableCollection<OpenEyeParameter>();
        
        //spilt input into line to avoid issues due to optional comma
        var lines = fileText.Split(["\r\n", "\r", "\n"], System.StringSplitOptions.None);

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            var paramMatch = parameterPattern.Match(rawLine);
            if (paramMatch.Success)
            {
                string name = paramMatch.Groups[1].Value;
                string value = paramMatch.Groups[2].Value;
                
                //check if name already exists in the list, might be redundant here
                if (!byName.TryGetValue(name, out var entry))
                {
                    entry = new OpenEyeParameter { Name = name };
                    byName[name] = entry;
                    result.Add(entry);
                }
                entry.Value = value;
                //continue;
            }

            var descMatch = descriptionPattern.Match(rawLine);
            if (descMatch.Success)
            {
                string name = descMatch.Groups[1].Value;
                string description = descMatch.Groups[2].Value;
                //check if Parameter of that name exists already
                if (!byName.TryGetValue(name, out var entry))
                {
                    continue;   //Skip this description if no value found!
                    /*
                    This would create a new Parameter, but we don't want description only Parameters!
                    entry = new OpenEyeParameter { Name = name };
                    byName[name] = entry;
                    result.Add(entry);
                    */
                }
                entry.Description = description;
                //Debug.WriteLine("Description for parameter "++" is "+description);
            }
        }

        return result;
    }

    /// <returns>true if parameters were read from the chosen file, false if nothing was loaded.</returns>
    public async Task<bool> LoadParametersFromFileAsync()
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var window = lifetime?.MainWindow;
        if (window?.StorageProvider is null)
            throw new InvalidOperationException("The main window is not available to choose a header file.");
        FilePickerFileType[] filetypes = [new FilePickerFileType("Verilog Header") { Patterns = ["*.vh"] }
        ];
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose OpenEye Header File",
            AllowMultiple = false,
            FileTypeFilter = filetypes
        });

        // The picker was cancelled.
        if (files.Count != 1) return false;

        var filepath = files[0].TryGetLocalPath();
        var fileText = await File.ReadAllTextAsync(filepath!);
        var parameters = ParseParameters(fileText);

        if (parameters.Count == 0)
        {
            StatusReporter.Warning($"No OpenEye parameters found in {Path.GetFileName(filepath)}. The current parameters were kept.");
            return false;
        }

        Parameters = parameters;
        return true;
    }

    public async Task GenerateHeaderAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("No path was given to save the header file.");

        await using StreamWriter sw = File.CreateText(path);
        await sw.WriteLineAsync("//This header was generated by the Systembuilder Plugin for Oneware Studio");
        await sw.WriteLineAsync("");
        foreach (var parameter in Parameters)
        {
            await sw.WriteLineAsync("parameter " + parameter.Name + "=" + parameter.Value + ",");    
        }
        
        await sw.WriteLineAsync("");
        
        foreach (var parameter in Parameters)
        {
            await sw.WriteLineAsync("description " + parameter.Name + " = \"" + parameter.Description + "\",");
        }
    }

    
    public async Task OpenEyeCall(string? pathToDir, string? pathToParams)
    {
        if (string.IsNullOrWhiteSpace(pathToDir)) return;

        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = "run " +
                        "--rm " +   //remove after execution(?)
                        $"-v \"{pathToDir}:/systembuilder/export\" " +  //create shared directory for file transfer 
                        $"-v \"{pathToParams}:/systembuilder/OpenEye/src/parameters.vh\" " +
                        "liteximg:latest " +
                        "sh -c " +                          // run command in shell
                        "\". .venv/bin/activate && " +      // activate virtual environment
                        "cd /systembuilder/OpenEye/src/ && " +  //change directory to location of python script
                        "python3 openeye_interface.py parameters.vh && " +  //run the python script         
                        "mkdir /systembuilder/export/OpenEye &&" +      //prepare folder in export directory
                        "cp -a /systembuilder/OpenEye/hdl /systembuilder/export/OpenEye\"" +    //copy relevant files
                        //there may be a need to copy more relevant files but this is just initial prototype
                        "\n",   // end of command
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
 