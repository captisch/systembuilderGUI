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
        get;
        set => field = ConfigFile.StorageProvider = value;
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
    private async Task AddSubModulesToConfig()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Moduledatei auswählen",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Verilog-Dateien"){ Patterns = new[] {"*.v"} }
            }.ToList()
            
        });
        
        foreach (var file in files)
        {
            await ConfigFile.AddSubModuleFromFile(file.TryGetLocalPath() ?? file.Path.LocalPath);
        }
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

    private async Task CopyExternalSources()
    {
        // Maybe create directory for external sources so everything is gathered in one place.
        // An existing directory could alos be wiped to clear unused Verilog files.
        
        List<string> externalSources = new List<string>();
        
        foreach (var file in ConfigFile.subModules)
        {
            string destinationFilePath;
            if (file.Source is not null)
            {
                var fileName = Path.GetFileName(file.Source);
                Debug.Assert(ConfigFile.OutputDirPath != null, "ConfigFile.OutputDirPath is null!");
                destinationFilePath = Path.Combine(ConfigFile.OutputDirPath, fileName);
            }
            else return;

            if (!externalSources.Contains(file.Source))
            {
                externalSources.Add(file.Source);
                try
                {
                    File.Copy(file.Source, destinationFilePath, overwrite: true);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            // Maybe update file.Source to new copy of the verilog file.
            // BUT there will be possible confusion over modules with same name but different source files. So maybe not.
        }
    }
    
    [RelayCommand]
    private async Task GenerateSystem()
    {
        await ConfigFile.GenerateLitexInput(projectPath);

        string? socName = ConfigFile.GetSOCName();
        
        await systemBuilder.call(ConfigFile.OutputFilePath, ConfigFile.OutputDirPath, ConfigFile.LogPath);

        await CopyExternalSources();
        
        WrapperBuilder wrapperBuilder = new WrapperBuilder(ConfigFile);
        wrapperBuilder.GenerateWrapper(projectPath);

        await ContainerLocator.Current.Resolve<IProjectExplorerService>().ReloadProjectAsync(activeProject);
    }
}