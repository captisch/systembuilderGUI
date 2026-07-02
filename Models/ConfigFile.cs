using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace systembuilderGUI.Models;

public partial class ConfigFile : ObservableObject
{
    public IStorageProvider? StorageProvider { get; set; }
    
    public ConfigFile()
    {
        CoreItems = loadItems(new Uri("avares://systembuilderGUI/Assets/configFile_items_definition.yaml"));
        Interfaces = loadItems(new Uri("avares://systembuilderGUI/Assets/configFile_interfaces_definition.yaml"));
    }
    public ObservableCollection<ConfigItem> CoreItems { get; set; } = new();
    
    public ObservableCollection<ConfigItem> Interfaces { get; set; } = new();
    
    public ObservableCollection<SubModule> subModules { get; set; } = new();
    
    [ObservableProperty] private string? outputDirPath;
    [ObservableProperty] private string? outputFilePath;
    
    private ObservableCollection<ConfigItem> loadItems(Uri source)
    {
        using var stream = AssetLoader.Open(source);
        using var reader = new StreamReader(stream);
        var yml = reader.ReadToEnd();

        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .Build();
        var returnItems = deserializer.Deserialize<ObservableCollection<ConfigItem>>(yml);

        foreach (var item in returnItems.Where(i => string.IsNullOrWhiteSpace(i.Value) && !string.IsNullOrWhiteSpace(i.DefaultValue)))
            item.Value = item.DefaultValue;
        
        return returnItems;
    }

    private string indentBy(int level)
    {
        return string.Concat(Enumerable.Repeat(" ", 4*level));
    }
    
    private string createOutput()
    {
        var yml = "";

        foreach (var item in CoreItems)
        {
            yml +=  item.Name + ": " + item.Value + "\n";
        }
        
        foreach (var item in Interfaces)
        {
            yml +=  item.Name + ": " + item.Value + "\n";
        }
        
        yml += "\nexternal_modules: {\n";

        foreach (var (index, module) in subModules.Index())
        {
            Instance helper = new Instance(module);
            yml += $"{indentBy(1)}\"ext_mod_{index}\":{{\n";
            var source = module.IsExternalModule ? "None" : $"{module.Filename}" ;
            yml += $"{indentBy(2)}\"source\": \"{source}\",\n";
            yml += $"{indentBy(2)}\"module_name\": \"{module.Module.Name}\",\n";
            yml += $"{indentBy(2)}\"instance_name\": \"{module.Instance}\",\n";
            yml += $"{indentBy(2)}\"parameters\": {{\n";
            foreach (var param in module.Module.Parameters)
            {
                yml += $"{indentBy(3)}\"{param.Name}\": {param.Value},\n";
            }
            yml += $"{indentBy(2)}}},\n";
            yml += $"{indentBy(2)}\"ports\": {{\n";
            foreach (var (index_port, port) in module.Module.Ports.Index())
            {
                if (module.IsExternalModule && !port.RouteToSOC) continue;
                yml += $"{indentBy(3)}\"port{index_port}\": {{\n";
                yml += $"{indentBy(4)}\"name\": \"{port.Name}\",\n";
                var direction = port.Direction.ToString().ToLower() == "input" ? "in" : 
                    port.Direction.ToString().ToLower() == "output" ? "out" :
                    port.Direction.ToString().ToLower();
                yml += $"{indentBy(4)}\"direction\": \"{direction}\",\n";
                yml += $"{indentBy(4)}\"size\": {helper.ParsePortSize(port.Width)}\n";
                yml += $"{indentBy(3)}}},\n";
            }
            yml += $"{indentBy(2)}}},\n";
            yml += $"{indentBy(1)}}},\n";
        }
        yml += "}\n";
        
        return yml;
    }
    
    private ConfigItem? FindItemByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return CoreItems.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? Interfaces.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public string? GetSOCName()
    {
        var nameItem = FindItemByName("Name");
        return nameItem?.Value;
    }

    
    public string Save(string? path = null)
    {
        var nameItem = FindItemByName("Name");
        if (nameItem is null) throw new InvalidOperationException("No name was found in the SOC config file.");
        
        if (string.IsNullOrWhiteSpace(path))
        {
            var rootPath = Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:\\";
            path = Path.Combine(rootPath, "fentwumsGUI", "systembuilder");
        }
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("No path was given to save the config file.");
        
        var saveFilePath = Path.Combine(path, $"configFile_{nameItem.Value}.yaml");
        
        OutputDirPath = path;
        OutputFilePath = saveFilePath;

        if (!Directory.Exists(Path.GetDirectoryName(saveFilePath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(saveFilePath));
        }

        var yml = createOutput();
        
        File.WriteAllText(saveFilePath, yml);
        Debug.WriteLine($"Saving to {saveFilePath}");
        return saveFilePath;
    }
    
    public async Task ChooseOutputDirectory(ConfigItem item)
    {
        if (item is null)
            return;

        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var window = lifetime?.MainWindow;
        if (window?.StorageProvider is null)
            return;

        var result = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Ordner auswählen",
            AllowMultiple = false
        });

        var folder = result?.FirstOrDefault();
        if (folder is not null)
        {
            var path = folder.TryGetLocalPath() ?? folder.Path.LocalPath;
            
            item.Value = path;
        }
    }
    
    public async Task AddSubModule()
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var window = lifetime?.MainWindow;
        if (window?.StorageProvider is null)
            return;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Moduledatei auswählen",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Verilog-Dateien"){ Patterns = new[] {"*.v"} }
            }.ToList()
            
        });
        var verilogParser = new VerilogParser();
        foreach (var file in files)
        {
            var verilogPath = file.TryGetLocalPath() ?? file.Path.LocalPath;
            #if DEBUG
            Console.WriteLine(verilogPath);
            #endif
            
            var modules = verilogParser.ReadVerilog(verilogPath);

            if (modules.Count > 0)
            {
                foreach (var module in modules)
                {
                    var instCnt = 0;
                    var instanceName = $"{module.Name}_{instCnt}";

                    while (subModules.Any(sm => sm.Instance == instanceName))
                    {
                        instCnt++;
                        instanceName = $"{module.Name}_{instCnt}";
                    }

                    subModules.Add(new SubModule()
                    {
                        Module = module,
                        Source = verilogPath,
                        Filename = Path.GetFileName(verilogPath),
                        Instance = instanceName,
                    });
                }
            }
        }
    }

    public async Task CopySubmodule(SubModule subModule)
    {
        var instanceName = subModule.Instance;
        var baseInstanceName = instanceName;
        var number = 0;

        // Prüfe ob der Instanzname mit Zahlen endet
        var match = System.Text.RegularExpressions.Regex.Match(instanceName, @"_(\d+)$");
        if (match.Success)
        {
            baseInstanceName = instanceName[..match.Index];
            number = int.Parse(match.Groups[1].Value);
        }
        else
        {
            baseInstanceName = instanceName;
        }

        // Generiere einen eindeutigen Namen
        do
        {
            number++;
            instanceName = $"{baseInstanceName}_{number}";
        } while (subModules.Any(m => m.Instance == instanceName));

        // Erstelle eine Kopie des Moduls
        var newModule = new SubModule
        {
            Source = subModule.Source,
            Filename = subModule.Filename,
            Module = subModule.Module.Copy(),
            Instance = instanceName,
            IsExternalModule = subModule.IsExternalModule
        };

        subModules.Add(newModule);
    }
}