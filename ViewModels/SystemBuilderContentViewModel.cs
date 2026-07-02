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
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using systembuilderGUI.Models;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization.NamingConventions;

namespace systembuilderGUI.ViewModels;

public partial class SystemBuilderContentViewModel : ViewModelBase
{
    private static readonly IProjectRoot? activeProject = ContainerLocator.Current.Resolve<IProjectExplorerService>().ActiveProject;
    private static string? projectPath = activeProject.RootFolderPath;
    
    [ObservableProperty]
    private ConfigFile configFile;
    
    private SystemBuilder systemBuilder;
    
    public SystemBuilderContentViewModel()
    {
        configFile = new();
        systemBuilder = new();
    }
    
    public IStorageProvider? StorageProvider
    {
        set => ConfigFile.StorageProvider = value;
    }

    [RelayCommand]
    private Task SaveConfig()
    {
        ConfigFile.Save(projectPath);
        return Task.CompletedTask;
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
        await SaveConfig();

        string? socName = ConfigFile.GetSOCName();
        
        await systemBuilder.call(ConfigFile.OutputFilePath, ConfigFile.OutputDirPath, ConfigFile.LogPath);
        
        WrapperBuilder wrapperBuilder = new WrapperBuilder(ConfigFile);
        wrapperBuilder.GenerateWrapper(projectPath);

        await ContainerLocator.Current.Resolve<IProjectExplorerService>().ReloadProjectAsync(activeProject);
    }
}