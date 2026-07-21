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

    public Dictionary<string, dynamic> ToConfig()
    {
        Dictionary<string, dynamic> dict = new();
        
        dict.Add("source", isExternalModule ? source : "None");
        dict.Add("module_name", module.Name);
        dict.Add("instance_name", instance);
        dict.Add("parameters", module.Parameters.ToDictionary(x => x.Name, x => x.Value));
        dict.Add("ports", module.Ports
            .Where(p => !(!p.RouteToSOC && isExternalModule))
            .Select((p, index) => new{p, index})
            .ToDictionary(
                x => "port" +  x.index,
                x => x.p.ToConfig(this)
                )
        );

        return dict;
    }
}