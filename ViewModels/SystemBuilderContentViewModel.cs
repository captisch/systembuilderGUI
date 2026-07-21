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
        await SaveConfig();

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