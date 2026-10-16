namespace systembuilderGUI.Models;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;

public static class VerilogParser
{
    private const string Ident = @"[A-Za-z_][A-Za-z0-9_$]*";

    private static readonly Regex ModuleRegex =
        new($@"\bmodule\s+(?<name>{Ident})", RegexOptions.Compiled);

    private static readonly Regex EndModuleRegex =
        new(@"\bendmodule\b", RegexOptions.Compiled);

    // Declarations inside the module body (up to the terminating ';')
    private static readonly Regex BodyDeclRegex =
        new(@"\b(?<kw>input|output|inout|parameter|localparam|reg|wire)\b[^;]*;",
            RegexOptions.Compiled);

    // One port item: [dir] [type] [signed] [range] name
    private static readonly Regex PortItemRegex = new(
        @"^(?:(?<dir>input|output|inout)\b\s*)?" +
        @"(?:(?<type>wire|reg|logic|tri|tri0|tri1|wand|wor|supply0|supply1|uwire|var)\b\s*)?" +
        @"(?:(?<signed>signed)\b\s*)?" +
        @"(?<width>(?:\[[^\]]*\]\s*)*)" +
        $@"(?<name>{Ident})",
        RegexOptions.Compiled);

    // One parameter item: [parameter|localparam] [type] [signed] [range] name [= value]
    private static readonly Regex ParamItemRegex = new(
        @"^(?:(?<kw>parameter|localparam)\b\s*)?" +
        @"(?:(?:integer|real|realtime|time|string|int|bit|logic|reg|byte|shortint|longint|shortreal|type)\b\s*)?" +
        @"(?:(?:signed|unsigned)\b\s*)?" +
        @"(?:\[[^\]]*\]\s*)?" +
        $@"(?<name>{Ident})\s*(?:=\s*(?<value>.*))?$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Parses all Verilog modules found in the given file.</summary>
    public static List<Module> ParseVerilogModules(string filePath)
    {
        string text = PreProcess(File.ReadAllText(filePath));
        var modules = new List<Module>();
        int searchFrom = 0;

        while (true)
        {
            var m = ModuleRegex.Match(text, searchFrom);
            if (!m.Success) break;

            var module = new Module
            {
                Name = m.Groups["name"].Value,
                Ports = new List<Port>(),
                Parameters = new List<Parameter>()
            };

            // ---- Header: optional #( parameters ) and optional ( ports ) ----
            int pos = m.Index + m.Length;
            string? paramText = null, portText = null;

            SkipWhitespace(text, ref pos);
            if (pos < text.Length && text[pos] == '#')
            {
                pos++;
                SkipWhitespace(text, ref pos);
                if (pos < text.Length && text[pos] == '(')
                    paramText = ReadParenthesized(text, ref pos, module.Name);
            }

            SkipWhitespace(text, ref pos);
            if (pos < text.Length && text[pos] == '(')
                portText = ReadParenthesized(text, ref pos, module.Name);

            int semicolon = text.IndexOf(';', pos);
            if (semicolon < 0) break; // malformed file, nothing more to parse

            // ---- Body: everything up to the matching endmodule ----
            int bodyStart = semicolon + 1;
            var end = EndModuleRegex.Match(text, bodyStart);
            int bodyEnd = end.Success ? end.Index : text.Length;
            string body = text.Substring(bodyStart, bodyEnd - bodyStart);

            // ---- Header parameters ----
            if (paramText != null)
                ParseParameterList(paramText, module.Parameters);

            // ---- Header ports ----
            bool ansiStyle = false;
            if (portText != null)
            {
                ansiStyle = Regex.IsMatch(portText.TrimStart(), @"^(input|output|inout)\b");
                if (ansiStyle)
                {
                    module.Ports.AddRange(ParseDeclarations(portText));
                }
                else
                {
                    // Non-ANSI: header only lists the names, details follow in the body
                    foreach (var item in SplitTopLevel(portText, ','))
                    {
                        string name = item.Trim();
                        if (Regex.IsMatch(name, $"^{Ident}$"))
                            module.Ports.Add(new Port { Name = name });
                    }
                }
            }

            // ---- Body declarations ----
            foreach (Match d in BodyDeclRegex.Matches(body))
            {
                string kw = d.Groups["kw"].Value;
                string statement = d.Value.TrimEnd(';');

                if (kw is "parameter" or "localparam")
                {
                    ParseParameterList(statement, module.Parameters);
                    continue;
                }

                if (ansiStyle) continue; // ports are already fully described by the header

                // Non-ANSI: complete the ports that were named in the header.
                // Names that aren't ports (e.g. internal regs, function inputs) are ignored.
                foreach (var decl in ParseDeclarations(statement))
                {
                    var port = module.Ports.FirstOrDefault(p => p.Name == decl.Name);
                    if (port == null) continue;

                    if (decl.Direction != PortDirections.none) port.Direction = decl.Direction;
                    if (decl.Type != PortTypes.none) port.Type = decl.Type;
                    if (decl.Signed) port.Signed = true;
                    if (decl.Width != null) port.Width = decl.Width;
                }
            }

            modules.Add(module);
            searchFrom = end.Success ? end.Index + end.Length : text.Length;
        }

        return modules;
    }

    // ------------------------------------------------------------------
    // Declaration parsing
    // ------------------------------------------------------------------

    /// <summary>
    /// Parses a comma-separated declaration such as
    /// "input wire [7:0] a, b, output reg signed c" into ports.
    /// Items without their own direction/type/range inherit them from the previous item.
    /// </summary>
    private static List<Port> ParseDeclarations(string text)
    {
        var result = new List<Port>();
        var dir = PortDirections.none;
        var type = PortTypes.none;
        bool isSigned = false;
        string? width = null;

        foreach (var raw in SplitTopLevel(text, ','))
        {
            var m = PortItemRegex.Match(raw.Trim());
            if (!m.Success) continue;

            bool hasDir = m.Groups["dir"].Success;
            string w = Regex.Replace(m.Groups["width"].Value, @"\s+", "");

            if (hasDir || m.Groups["type"].Success || m.Groups["signed"].Success || w.Length > 0)
            {
                if (hasDir) dir = ParseDirection(m.Groups["dir"].Value);
                type = ParseType(m.Groups["type"].Value);
                isSigned = m.Groups["signed"].Success;
                width = w.Length > 0 ? w : "1";
            }
            Debug.WriteLine("Width of Port "+ m.Groups["name"].Value + "is"+ width);
            result.Add(new Port
            {
                Name = m.Groups["name"].Value,
                Direction = dir,
                Type = type,
                Signed = isSigned,
                Width = width
            });
        }

        return result;
    }

    /// <summary>
    /// Parses "parameter A = 1, B = 2" or the items of a #( ... ) list.
    /// localparams are skipped, since they cannot be set from outside.
    /// </summary>
    private static void ParseParameterList(string text, List<Parameter> target)
    {
        bool isLocal = false;

        foreach (var raw in SplitTopLevel(text, ','))
        {
            string item = raw.Trim();
            if (item.Length == 0) continue;

            var m = ParamItemRegex.Match(item);
            if (!m.Success) continue;

            if (m.Groups["kw"].Success)
                isLocal = m.Groups["kw"].Value == "localparam";
            if (isLocal) continue;

            target.Add(new Parameter
            {
                Name = m.Groups["name"].Value,
                Value = m.Groups["value"].Success ? m.Groups["value"].Value.Trim() : null
            });
        }
    }

    private static PortDirections ParseDirection(string s) => s switch
    {
        "input" => PortDirections.input,
        "output" => PortDirections.output,
        "inout" => PortDirections.inout,
        _ => PortDirections.none
    };

    // The enum only knows wire and reg; other net/variable types (logic, tri, ...) map to none.
    private static PortTypes ParseType(string s) => s switch
    {
        "wire" => PortTypes.wire,
        "reg" => PortTypes.reg,
        _ => PortTypes.none
    };

    // ------------------------------------------------------------------
    // Text helpers
    // ------------------------------------------------------------------

    private static string PreProcess(string src)
    {
        string text = StripComments(src);
        // Remove attributes like (* keep *), but not the event control @(*)
        return Regex.Replace(text, @"\(\*(?!\s*\)).*?\*\)", " ", RegexOptions.Singleline);
    }

    private static string StripComments(string src)
    {
        var sb = new StringBuilder(src.Length);
        int i = 0;

        while (i < src.Length)
        {
            char c = src[i];

            if (c == '"') // copy string literals untouched
            {
                sb.Append(c);
                i++;
                while (i < src.Length && src[i] != '"')
                {
                    if (src[i] == '\\' && i + 1 < src.Length) sb.Append(src[i++]);
                    sb.Append(src[i++]);
                }
                if (i < src.Length) sb.Append(src[i++]);
            }
            else if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
            {
                while (i < src.Length && src[i] != '\n') i++;
            }
            else if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
            {
                int end = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? src.Length : end + 2;
                sb.Append(' ');
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }

        return sb.ToString();
    }

    private static void SkipWhitespace(string s, ref int pos)
    {
        while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
    }

    /// <summary>Reads a balanced "( ... )" block starting at pos and returns the inner text.</summary>
    private static string ReadParenthesized(string s, ref int pos, string moduleName)
    {
        int start = pos;
        int depth = 0;

        for (; pos < s.Length; pos++)
        {
            if (s[pos] == '(') depth++;
            else if (s[pos] == ')' && --depth == 0)
            {
                string inner = s.Substring(start + 1, pos - start - 1);
                pos++;
                return inner;
            }
        }

        throw new FormatException($"Unbalanced parentheses in the header of module '{moduleName}'.");
    }

    /// <summary>Splits at the separator, ignoring separators inside (), [], {} and string literals.</summary>
    private static IEnumerable<string> SplitTopLevel(string text, char separator)
    {
        int depth = 0;
        bool inString = false;
        var sb = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inString)
            {
                sb.Append(c);
                if (c == '\\' && i + 1 < text.Length) sb.Append(text[++i]);
                else if (c == '"') inString = false;
                continue;
            }

            if (c == '"') { inString = true; sb.Append(c); continue; }

            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') depth--;

            if (c == separator && depth == 0)
            {
                yield return sb.ToString();
                sb.Clear();
            }
            else sb.Append(c);
        }

        yield return sb.ToString();
    }
}