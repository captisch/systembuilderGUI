using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace systembuilderGUI.Models;

enum ConfigItemTypes
{
    String,
    Integer,
    Boolean,
    Float,
    Enum
};

public partial class ConfigItem : ObservableObject
{
    private string? _value;

    public string? Value
    {
        get => _value;
        set
        {
            Debug.WriteLine($"entry:\"{value}\"");
            var trimmedValue = value?.Trim();
            Debug.WriteLine($"after trim:\"{value}\"");
            var validtedValue = ValidateEntry(trimmedValue);
            Debug.WriteLine($"after validation:\"{validtedValue}\"");

            if (validtedValue != _value)
            {
                _value = validtedValue;
                Debug.WriteLine($"assigned after validation: private:\"{_value}\" public:\"{Value}\"");
                OnPropertyChanged();
            }
            else if (value != _value)
            {
                Debug.WriteLine($"Forcing UI update");
                OnPropertyChanged();
            }
        }
    }

    private string ValidateEntry(string? entry)
    {
        switch (Type)
        {
            case "OpenString":
                if (string.IsNullOrWhiteSpace(entry) && !string.IsNullOrWhiteSpace(DefaultValue)) entry = DefaultValue;
                return entry;
            case "RestrictedString":
                return entry;
            case "Integer":
                if (int.TryParse(entry, NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out int intValue))
                {
                    Debug.WriteLine($"Parsed {entry} to {intValue}");
                    return entry;
                }
                else if (!string.IsNullOrWhiteSpace(DefaultValue))
                {
                    return DefaultValue;
                }
                else return string.Empty;
            case "Boolean":
                return entry;
            case "FilePath":
                return entry;
            case "Enum":
                return entry;
        }
        return string.Empty;
    }
    
    [ObservableProperty]
    public string? name;
    
    [ObservableProperty]
    public string? defaultValue;
    
    [ObservableProperty]
    public string? type;
    
    [ObservableProperty]
    public string? description;
    
    [ObservableProperty]
    public bool? access;
    
    public ObservableCollection<string?> options { get; set; } = new();
    
    partial void OnDefaultValueChanged(string? oldValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(Value) && !string.IsNullOrWhiteSpace(newValue))
        {
            Value = newValue;
        }
    }
}

    