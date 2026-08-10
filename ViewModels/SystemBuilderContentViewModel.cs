using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
    private async Task SaveConfigAsync()
    {
        await ConfigFile.SaveConfiguration(projectPath);
        return;
    }

    [RelayCommand]
    private async Task LoadConfigAsync()
    {
        await ConfigFile.LoadConfiguration();
        return;
    }

    [RelayCommand]
    private async Task ChooseOutputDirectoryOfConfig(ConfigItem item)
    {
        await ConfigFile.ChooseOutputDirectory(item);
        return;
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
        ConfigFile.GenerateLitexInput(projectPath);

        string? socName = ConfigFile.GetSOCName();
        
        await systemBuilder.call(ConfigFile.OutputFilePath, ConfigFile.OutputDirPath, ConfigFile.LogPath);

        List<string> externalSources = new List<string>();
        
        foreach (var file in ConfigFile.subModules)
        {
            if (file.Source != null && !externalSources.Contains(file.Source))
            {
                externalSources.Add(file.Source);
                
                string fileName = Path.GetFileName(file.Source);
                Debug.Assert(ConfigFile.OutputDirPath != null, "ConfigFile.OutputDirPath is null!");
                string destinationFilePath = Path.Combine(ConfigFile.OutputDirPath, fileName);

                File.Copy(file.Source, destinationFilePath);
            }
        }
        
        WrapperBuilder wrapperBuilder = new WrapperBuilder(ConfigFile);
        wrapperBuilder.GenerateWrapper(projectPath);

        await ContainerLocator.Current.Resolve<IProjectExplorerService>().ReloadProjectAsync(activeProject);
    }
}