using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using systembuilderGUI.Models;

namespace systembuilderGUI.Models;

public sealed class ConfigItemTemplateSelector : IDataTemplate
{
    
    public IDataTemplate? OpenString { get; set; }
    public IDataTemplate? Boolean { get; set; }
    public IDataTemplate? OptionList { get; set; }
    public IDataTemplate? Integer { get; set; }
    public IDataTemplate? FilePath { get; set; }
    public IDataTemplate? Default { get; set; }

    public Control? Build(object? param)
    {
        if (param is not ConfigItem item)
            return Default?.Build(param);

        var type = item.Type?.Trim();
        
        return type?.Equals("OpenString", StringComparison.OrdinalIgnoreCase) == true ? OpenString?.Build(param)
            : type?.Equals("Boolean", StringComparison.OrdinalIgnoreCase) == true ? Boolean?.Build(param)
            : type?.Equals("OptionList", StringComparison.OrdinalIgnoreCase) == true ? OptionList?.Build(param)
            : type?.Equals("Integer", StringComparison.OrdinalIgnoreCase) == true ? Integer?.Build(param)
            :type?.Equals("FilePath", StringComparison.OrdinalIgnoreCase) == true ? FilePath?.Build(param)
            : Default?.Build(param);
        
    }

    public bool Match(object? data) => data is ConfigItem;
}
