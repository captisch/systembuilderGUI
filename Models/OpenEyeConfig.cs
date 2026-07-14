using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;

namespace systembuilderGUI.Models;

public partial class OpenEyeConfig : ObservableObject
{
    public OpenEyeConfig(string templateFile)
    {
        Parameters = ReadParameters(new Uri("avares://systembuilderGUI/Assets/openEyeParameterTemplate.vh"));
    }
    
    public ObservableCollection<Parameter> Parameters { get; set; }

    private ObservableCollection<Parameter> ReadParameters(Uri templateFile)
    {
        using var stream = AssetLoader.Open(templateFile);
        using var reader = new StreamReader(stream);
        var verilogHeader = reader.ReadToEnd();
        
        var parameters = new ObservableCollection<Parameter>();
        if (!System.IO.File.Exists(verilogHeader) || string.IsNullOrWhiteSpace(verilogHeader) ||
            !verilogHeader.EndsWith(".vh"))
        {
            Debug.WriteLine("The requested header file does not exist or is invalid!");
            return parameters;
        }

        string headerText = System.IO.File.ReadAllText(verilogHeader);
        
        var regexPatternParameterlist = @"(?:#\s*\(\s*(?<parameterlist>[\s\S]*?)\s*\)\s*)";
        
        var matches = Regex.Matches(headerText, regexPatternParameterlist);

        foreach (Match match in matches)
        {
            var parameterlist = match.Groups["parameterlist"].Value;
            var parameterText = parameterlist.Split(',')
                .Select(p => p.Replace("parameter ", "").Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p));
            
            foreach (var parameter in parameterText)
            {
                var parameterName = parameter.Split('=')[0].Trim();
                var parameterValue = parameter.Split('=')[1].Trim();
                parameters.Add(new Parameter 
                {
                    Name = parameterName, 
                    Value = parameterValue
                });
            }
        }
        return parameters;
    }

    public void GenerateHeader(string targetDirectory)
    {
        //This method will generate the Verilog header file
        //Can probably be quite simple, something like println("parameter" +name+ "=" +value + ",")
        //The most complicated part will be the handling of files and paths
        string outputPath = Path.Combine(targetDirectory, "parameters.vh");

        using StreamWriter sw = File.CreateText(outputPath);
        sw.WriteLine("//This header has been created automatically");
        sw.WriteLine("");
        foreach (var parameter in Parameters)
        {
            sw.WriteLine("parameter " + parameter.Name + "=" + parameter.Value + ",");    
        }
    }
}