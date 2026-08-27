using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace systembuilderGUI.Models;

public partial class SubModule : ObservableObject
{
    [ObservableProperty] private string? source;
    
    [ObservableProperty] private string? filename;
    
    [ObservableProperty] private Module? module;

    [ObservableProperty] private string? instance;
    
    [ObservableProperty] private bool isExternalModule;

    public Dictionary<string, dynamic> ToDictionary(string outputType)
    {
        Dictionary<string, dynamic> dict = new();
        
        dict.Add("source", outputType == "save" ? source : isExternalModule ? source : "None"); // Maybe always hand over source path and external flag, would need adjustment in generator script
        dict.Add("module_name", module.Name);
        dict.Add("instance_name", instance);
        dict.Add("parameters", module.Parameters.ToDictionary(x => x.Name, x => x.Value));
        if (outputType == "build")
        {
            dict.Add("ports", module.Ports
                .Where(p => !(!p.RouteToSOC && isExternalModule))
                .Select((p, index) => new { p, index })
                .ToDictionary(
                    x => "port" + x.index,
                    x => x.p.ToDictionary(this, outputType)
                )
            );
        }
        else if (outputType == "save")
        {
            dict.Add("isExternalModule", isExternalModule);                                     // See above
            dict.Add("ports", module.Ports
                .Select((p, index) => new { p, index })
                .ToDictionary(
                    x => "port" + x.index,
                    x => x.p.ToDictionary(this, outputType)
                )
            );
        }

        return dict;
    }
}