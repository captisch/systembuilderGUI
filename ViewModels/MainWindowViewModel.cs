using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization.NamingConventions;
using YamlProcessing.Models;

namespace YamlProcessing.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ConfigFile configFile;
    
    private SystemBuilder systemBuilder;
    
    public MainWindowViewModel()
    {
        configFile = new();
        systemBuilder = new();
    }

    [RelayCommand]
    private Task SaveConfig()
    {
        return ConfigFile.Save();
    }

    [RelayCommand]
    private Task ChooseOutputDirectoryOfConfig(ConfigItem item)
    {
        return ConfigFile.ChooseOutputDirectory(item);
    }

    [RelayCommand]
    private Task AddSubModulesToConfig()
    {
        return ConfigFile.AddSubModule();
    }

    [RelayCommand]
    private Task RemoveSubModuleFromConfig(SubModule subModule)
    {
        ConfigFile.subModules.Remove(subModule);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task CopySubModule(SubModule subModule)
    {
        return ConfigFile.CopySubmodule(subModule);
    }

    [RelayCommand]
    private async Task GenerateSystem()
    {
        ConfigFile.Save();
        
        systemBuilder.call(ConfigFile.OutputPath);
    }
}