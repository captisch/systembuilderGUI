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
using systembuilderGUI.Services;
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
        await StatusReporter.RunAsync("Saving configuration...",
            () => ConfigFile.SaveConfigurationAsync(projectPath),
            "Configuration saved.", "Could not save the configuration.");
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
        
        var file = files.FirstOrDefault();
        if (file is null) return; // picker was cancelled

        var path = file.TryGetLocalPath() ?? file.Path.LocalPath;
        await StatusReporter.RunAsync("Loading configuration...",
            () => ConfigFile.LoadConfigurationAsync(path),
            $"Configuration loaded from {Path.GetFileName(path)}.", "Could not load the configuration.");
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
            var path = file.TryGetLocalPath() ?? file.Path.LocalPath;
            try
            {
                var added = await ConfigFile.AddSubModuleFromFileAsync(path);
                if (added is null || added.Count == 0)
                    StatusReporter.Warning($"No Verilog module found in {Path.GetFileName(path)}.");
            }
            catch (Exception e)
            {
                StatusReporter.Error($"Could not read modules from {Path.GetFileName(path)}.", e);
            }
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
        return ConfigFile.CopySubmoduleAsync(subModule);
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
            else continue;

            if (!externalSources.Contains(file.Source))
            {
                externalSources.Add(file.Source);
                File.Copy(file.Source, destinationFilePath, overwrite: true);
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
    
    [RelayCommand]
    private async Task MakeOpenEyeAsync()
    {
        var path = Path.Combine(projectPath, "parameters.vh");
        await OpenEyeConfig.GenerateHeaderAsync(path);
    
        await OpenEyeConfig.OpenEyeCall(projectPath, path);
        //For now I will leave it ot the user to add the interface for the OpenEye
        //This is done by choosing a non-zero value for oe_dma_width
    }

    [RelayCommand]
    private async Task AddOpenEyeToSoc()
    {
        /*
         * This needs to do the following things:
         * 1. Retrieve relevant information (currently: Is OpenEye prepared? What's the dma width?) 
         * 2. Set oe_dma_width value accordingly
         * 3. Add instance of OpenEye_FPGA to SoC-Wrapper
         */
        var path = Path.Combine(projectPath, "parameters.vh");
        if (File.Exists(path))
        {
            //Retrieve Value for DMA width
            var stream = File.OpenRead(path);
            using var reader = new StreamReader(stream);
            var headerText = await reader.ReadToEndAsync();
            var paramVals = OpenEyeConfig.ParseParameters(headerText);
            
            foreach (var value in paramVals)
            {
                if (value.Name == "DMA_BITWIDTH")
                {
                    ConfigFile.FindItemByName("oe_dma_width").Value = value.Value;
                }
            }
            //The wrapper builder can now see the value in the config and add the OE Instance
        }
        else
        { 
            //TODO: Add some form of obvious Notification to the User
            Debug.WriteLine("Could not find parameter file for OpenEye!");
        };
    }

    [RelayCommand]
    private async Task GenerateSystem()
    {
        using var process = StatusReporter.BeginProcess("Saving configuration...");
        try
        {
            await ConfigFile.SaveConfigurationAsync(projectPath);

            process.Update("Preparing SystemBuilder input...");
            await ConfigFile.GenerateSystemBuilderInput(projectPath);

            process.Update("Running SystemBuilder in Docker (this can take a while)...");
            await systemBuilder.call(ConfigFile.OutputFilePath, ConfigFile.OutputDirPath, ConfigFile.LogPath);

            process.Update("Copying external sources...");
            await CopyExternalSources();

            process.Update("Generating wrapper...");
            WrapperBuilder wrapperBuilder = new WrapperBuilder(ConfigFile);
            wrapperBuilder.GenerateWrapper(projectPath);

            await ContainerLocator.Current.Resolve<IProjectExplorerService>().ReloadProjectAsync(activeProject);
            StatusReporter.Success($"System '{ConfigFile.GetSOCName()}' generated.");
        }
        catch (Exception e)
        {
            process.FinishMessage = "Failed";
            StatusReporter.Error("System generation failed.", e);
        }
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
        
        StatusReporter.Info("All Parameters reset to default values. Removed all Submodules.");
        
        return Task.CompletedTask;
    }
}