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
    
    partial void OnIsExternalModuleChanged(bool value)
    {
        if (!value && Module?.Ports != null)
        {
            foreach (var port in Module.Ports)
            {
                port.RouteToTopmodule = true;
            }
        }
    }

}