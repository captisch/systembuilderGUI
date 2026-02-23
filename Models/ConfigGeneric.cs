using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace systembuilderGUI.Models;

public partial class ConfigGeneric : ObservableObject
{
    public ConfigGeneric()
    {
        //tbd
    }
    
    public ObservableCollection<ConfigItem> Items { get; set; } = new();
    
    [ObservableProperty] private string? outputDirPath;
    [ObservableProperty] private string? outputFilePath;
    
    
}