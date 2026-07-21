using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;

namespace systembuilderGUI.Models;

public partial class OpenEyeConfig : ObservableObject
{
    public OpenEyeConfig()
    {
        Parameters = ReadParameters(new Uri("avares://systembuilderGUI/Assets/openEyeParameterTemplate.vh"));
    }
    
    public ObservableCollection<Parameter> Parameters { get; set; }

    private ObservableCollection<Parameter> ReadParameters(Uri templateFile)
    {
        
        using var stream = AssetLoader.Open(templateFile);
        using var reader = new StreamReader(stream);
        var headerText = reader.ReadToEnd();
        
        
        var parameters = new ObservableCollection<Parameter>();
        if (string.IsNullOrWhiteSpace(headerText))
        {
            Debug.WriteLine("The requested header file does not exist or is invalid!");
            return parameters;
        }
        
        var regexPatternParameterlist = @"(?:parameter(?<parameterlist>\s*\S*\s*=\s*\S*))";
        
        var matches = Regex.Matches(headerText, regexPatternParameterlist);
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
                
                Debug.WriteLine("Found Parameter: "+parameterName);
                
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

    public void GenerateHeader(string? targetDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory)) throw new InvalidOperationException("No path was given to save the header file.");
        
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