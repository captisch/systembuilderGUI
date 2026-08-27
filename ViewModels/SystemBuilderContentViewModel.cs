using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    private ConfigFile configFile = new();
    
    [ObservableProperty] private OpenEyeConfig openEyeConfig = new ();
    
    private readonly SystemBuilder systemBuilder = new();

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
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Konfigurationsdatei auswählen",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("YAML-Dateien"){ Patterns = new[] {"*.yaml", "*.yml"} }
            }.ToList()
        });
        
        await ConfigFile.LoadConfiguration(files.FirstOrDefault()?.TryGetLocalPath() ?? files.FirstOrDefault()?.Path.LocalPath);
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
    private async Task SaveOpenEyeHeaderAsync()
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var window = lifetime?.MainWindow;
        if (window?.StorageProvider is null)
            return;
        var location = await window.StorageProvider.TryGetFolderFromPathAsync(new Uri(projectPath));
        FilePickerFileType[] filetypes = [new FilePickerFileType("Verilog Header") { Patterns = ["*.vh"] }
        ];
        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "PLACEHOLDER TITLE",
            SuggestedStartLocation = location,
            FileTypeChoices = filetypes,
            DefaultExtension = ".vh",
            SuggestedFileName = "parameters",
            ShowOverwritePrompt = true
        });
        
        if (file != null) await OpenEyeConfig.GenerateHeaderAsync(file.TryGetLocalPath());
        else Debug.WriteLine("Could not save parameter header file for OpenEye!");
    }

    [RelayCommand]
    private async Task LoadOpenEyeParamsAsync()
    {
         await OpenEyeConfig.LoadParametersFromFileAsync();
         OpenEyeConfig = new OpenEyeConfig(OpenEyeConfig.Parameters);
    }
    
    /* Derzeit auf Eis wegen konzeptioneller Probleme
     [RelayCommand]
    private async Task MakeOpenEyeAsync()
    {
        await OpenEyeConfig.GenerateHeaderAsync(projectPath);
    //This may need some rework for the paths
        var path = Path.Combine(projectPath, $"parametrs.vh");
        await OpenEyeConfig.OpenEyeCall(path);
    }*/

    [RelayCommand]
    private async Task GenerateSystem()
    {
        await ConfigFile.SaveConfiguration(projectPath);
        
        await ConfigFile.GenerateSystemBuilderInput(projectPath);

        string? socName = ConfigFile.GetSOCName();
        
        await systemBuilder.call(ConfigFile.OutputFilePath, ConfigFile.OutputDirPath, ConfigFile.LogPath);

        await CopyExternalSources();
        
        WrapperBuilder wrapperBuilder = new WrapperBuilder(ConfigFile);
        wrapperBuilder.GenerateWrapper(projectPath);

        await ContainerLocator.Current.Resolve<IProjectExplorerService>().ReloadProjectAsync(activeProject);
    }

    [RelayCommand]
    private Task ResetDefaults()
    {
        foreach (var item in ConfigFile.CoreItems)
        {
            item.Value = item.DefaultValue ?? string.Empty;
        }
        
        foreach (var item in ConfigFile.Interfaces)
        {
            item.Value = item.DefaultValue ?? string.Empty;
        }
        
        ConfigFile.subModules.Clear();
        
        return Task.CompletedTask;
    }
}