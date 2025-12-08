using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YamlProcessing.Models;

public class Module
{
    public string Name { get; set; }
    public List<Port>? Ports { get; set; }
    public string? Logic { get; set; }
    public List<Parameter> Parameters { get; set; }

    public Module Copy()
    {
        return new Module
        {
            Name = this.Name,
            Logic = this.Logic,
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
                    RouteToTopmodule = p.RouteToTopmodule
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
    [ObservableProperty] private string width;
    [ObservableProperty] private string? name;
    [ObservableProperty] private bool routeToTopmodule = true;
}

public partial class Parameter : ObservableObject
{
    [ObservableProperty] private string? name;
    [ObservableProperty] private string? value;
}

public class VerilogParser
{
    public List<Module> ReadVerilog(string verilogFile)
    {
        var modules = new List<Module>();
        if (!System.IO.File.Exists(verilogFile)) return modules;
        if (string.IsNullOrWhiteSpace(verilogFile)) return modules;
        if (!verilogFile.EndsWith(".v")) return modules;
        
        
        string verilogText = System.IO.File.ReadAllText(verilogFile);
        
        var patternSingleLineComment = @"//.*";
        var patternMultiLineComment = @"/\*[\s\S]*?\*/";
        
        var verilogTextNoComments = Regex.Replace(
            (Regex.Replace (verilogText, patternMultiLineComment, "")),
            patternSingleLineComment, "");
        
        var regexPatternParameterlist = @"(?:#\s*\(\s*(?<parameterlist>[\s\S]*?)\s*\)\s*)";
        var regexPatternPortlist = @"(?:\(\s*(?<portlist>[\s\S]*?)\s*\)\s*)";
        var regexPatternModuledeclaration = @$"(?:module\s+(?<modulename>\w+)\s*{regexPatternParameterlist}??{regexPatternPortlist}??;)";
        var regexPatternModule = $@"(?<module>{regexPatternModuledeclaration}+?(?<logic>[\s\S]*?)endmodule)";
        
        var regexPatternPortdeclaration = @"(?:\s*(?<direction>inout|output|input)?\s*(?<type>reg|wire)?\s*(?<signed>signed)?\s*(?<width>\[[^\]]+\])?\s*(?<name>\w+)\s*)";

        var matchModule = Regex.Matches(verilogTextNoComments, regexPatternModule);

        foreach (Match match in matchModule)
        {
            var modulename = match.Groups["modulename"].Value;
            var modulelogic = match.Groups["logic"].Value;
            
            var moduleparameters = new List<Parameter>();
            var parameterlist = match.Groups["parameterlist"].Value;
            
            var parameters = parameterlist.Split(',')
                .Select(p => p.Replace("parameter ", "").Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p));

            foreach (var parameter in parameters)
            {
                var parameterName = parameter.Split('=')[0].Trim();
                var parameterValue = parameter.Split('=')[1].Trim();
                moduleparameters.Add(new Parameter 
                    {
                        Name = parameterName, 
                        Value = parameterValue
                    });
            }
            
            var moduleports = new List<Port>();
            var portlist = match.Groups["portlist"].Value;
            var ports = portlist.Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p));

            PortDirections lastPortDirection = PortDirections.none;
            PortTypes lastPortType = PortTypes.none;
            string lastPortWidth = "1";
            bool lastIsSigned = false;

            foreach (var port in ports)
            {
                var matchPortDeclaration = Regex.Match(port, regexPatternPortdeclaration);
                
                if (matchPortDeclaration.Success)
                {
                    var directionStr = matchPortDeclaration.Groups["direction"].Value;
                    var typeStr = matchPortDeclaration.Groups["type"].Value;
                    var signedStr = matchPortDeclaration.Groups["signed"].Value;
                    var widthStr = matchPortDeclaration.Groups["width"].Value;
                    var nameStr = matchPortDeclaration.Groups["name"].Value;
                    
                    PortDirections portDirection;
                    PortTypes portType;
                    string portWidth;
                    bool isSigned;
                    

                    if (!string.IsNullOrWhiteSpace(directionStr) && Enum.TryParse(directionStr, out PortDirections parsedDirection))
                    {
                        portDirection = parsedDirection;
                        lastPortDirection = portDirection;
                        lastPortType = PortTypes.wire;      // reset type to wire, if direction is changed
                        lastIsSigned = false;                // teset signed flag, if direction is changed
                        lastPortWidth = "1";                // reset width to 1, if direction is changed
                    }
                    else
                    {
                        portDirection = lastPortDirection;
                    }
                        

                    if (!string.IsNullOrWhiteSpace(typeStr) && Enum.TryParse(typeStr, out PortTypes parsedType))
                    {
                        portType = parsedType;
                        lastPortType = portType;
                        lastIsSigned = false;               // reset signed flag, if type is changed
                        lastPortWidth = "1";                // reset width to 1, if type is changed
                    }
                    else
                    {
                        portType = lastPortType;
                    }

                    if (!string.IsNullOrWhiteSpace(signedStr))
                    {
                        isSigned = true;
                        lastIsSigned = isSigned;
                        lastPortWidth = "1";                // reset width to 1, if signed is changed
                    }
                    else
                    {
                        isSigned = lastIsSigned;
                    }

                    if (!string.IsNullOrWhiteSpace(widthStr))
                    {
                        portWidth = widthStr;
                        lastPortWidth = portWidth;
                    }
                    else
                    {
                        portWidth = lastPortWidth;
                    }

                    moduleports.Add(new Port
                    {
                        Direction = portDirection,
                        Type = portType,
                        Width = portWidth,
                        Name = nameStr,
                        Signed = isSigned
                    });
                }
            }
            
            modules.Add(new Module
            {
                Name = modulename,
                Parameters = moduleparameters,
                Ports = moduleports,
                Logic = modulelogic
            });
        }
        
        return modules;
    }
    
}
