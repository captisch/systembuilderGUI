using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;

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
    
    private ObservableCollection<OpenEyeParameter> ParseParameters(string? fileText)
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
                
                //check if name already exists in the list, probably redundant here
                //TODO: CHeck if the construct checking for existing element of that name is required!
                if (!byName.TryGetValue(name, out var entry))
                {
                    entry = new OpenEyeParameter { Name = name };
                    byName[name] = entry;
                    result.Add(entry);
                }
                entry.Value = value;
                continue;
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
            }
        }

        return result;
        /*old version, keep around until new version is successfully tested!!
        var parameters = new ObservableCollection<OpenEyeParameter>();
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
                
                parameters.Add(new OpenEyeParameter 
                {
                    Name = parameterName, 
                    Value = parameterValue
                });
            }
        }
        Debug.WriteLine("If there are no parameters listed before this message, something went wrong.");
        return parameters;*/
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