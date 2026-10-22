using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace systembuilderGUI.Models;

public class Module
{
    public string Name { get; set; }
    public List<Port>? Ports { get; set; }
    public List<Parameter>? Parameters { get; set; }

    public Module Copy()
    {
        return new Module
        {
            Name = this.Name,
            Parameters = this.Parameters
                .Select(p => new Parameter { Name = p.Name, Value = p.Value })
                .ToList(),
            Ports = this.Ports?
                .Select(p => new Port
                {
                    Direction = p.Direction,
                    Type = p.Type,
                    Signed = p.Signed,
                    Width = p.Width,
                    Name = p.Name,
                    RouteToSOC = p.RouteToSOC,
                    RouteToWrapper = p.RouteToWrapper,
                    NoRoute = p is { RouteToSOC: false, RouteToWrapper: false }
                })
                .ToList()
        };
    }
}

public enum PortTypes{
    none,
    wire,
    reg
}

public enum PortDirections{
    none,
    input,
    output,
    inout,
}

public partial class Port : ObservableObject
{
    [ObservableProperty] private PortDirections direction;
    [ObservableProperty] private PortTypes type;
    [ObservableProperty] private bool signed;
    [ObservableProperty] private string? width;
    [ObservableProperty] private string? name;
    [ObservableProperty] private bool routeToSOC = false;
    [ObservableProperty] private bool routeToWrapper = false;
    [ObservableProperty] private bool noRoute = true;

    public Dictionary<dynamic, dynamic> ToDictionary(SubModule submodule, string outputType)
    {
        var helper = new Instance(submodule);
        Dictionary<dynamic, dynamic> dict = new();
        dict.Add("name", Name);

        if (outputType == "build")
        {
            dict.Add("direction",
                direction == PortDirections.input ? "in" : direction == PortDirections.output ? "out" : "inout");
            dict.Add("size", helper.ParsePortSize(width));
        }else if (outputType == "save")
        {
            dict.Add("routeToSOC", routeToSOC);
            dict.Add("routeToWrapper", routeToWrapper);
        }
        else
        {
            throw new InvalidOperationException($"Unknown output type: {outputType}");
        }
        return dict;
    }
    
    public void SetRoute(bool routeToSOC, bool routeToWrapper)
    {
        RouteToSOC = routeToSOC;
        RouteToWrapper = routeToWrapper;
        NoRoute = !routeToSOC && !routeToWrapper;
    }
}

public partial class Parameter : ObservableObject
{
    [ObservableProperty] private string? name;
    [ObservableProperty] private string? value;
}
