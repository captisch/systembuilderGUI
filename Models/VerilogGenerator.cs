using System;
using System.Collections.Generic;
using System.IO;
//using SystemBuilder.Types;

namespace systembuilderGUI.Models;
//Maybe this should be put elsewhere in the project structure...

/*
Feature Outline:
-Generate new top module as wrapper for the SoC and added modules, consisting of:
    -Instances and known connections
    -(Known) Ports of the new top module and their connections    
-Generated code should be as human-readable as possible
*/

public class VerilogGenerator(List<Instance> instances, Module topModule, List<Wire> wires)
{
    //This implementation assumes a valid list of instances and TopModule are passed.

    private List<Instance> Instances { get; } = instances;
    private Module TopModule { get; } = topModule;
    private List<Wire> ConWires { get; } = wires; 
    
    public void GenerateVerilog(string targetDirectory)
    {
        string outputPath = Path.Combine(targetDirectory, TopModule.Name + ".v");

        /*if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
        File.Create(outputPath);
        */
        //DEBUG:
        Console.WriteLine("Generating verilog file");
        
        using (StreamWriter sw = File.CreateText(outputPath))
        {
            sw.WriteLine("//This module has been created automatically");
            sw.WriteLine("");
            sw.WriteLine("module " + TopModule.Name + " #(");  
        }
        
        //DEBUG
        Console.WriteLine("Made it past the first sw!");
        
        //TopModule shall not have Parameters
        AddParams(outputPath, TopModule.Parameters);
        AddPorts(outputPath, TopModule.Ports);
        AddWires(outputPath, ConWires);
        
        foreach (Instance currentInstance in Instances)
        {
            AddModuleInstance(outputPath, currentInstance);
        }
    }
    
    //auxiliary methods for organization
    static void AddParams(string outputPath, List<Parameter>? parameters)
    {
        using StreamWriter sw = File.AppendText(outputPath);
        //going through all params, last line is different (no ',' at the end)
        int numberOfParams = 0;
        if (parameters != null) numberOfParams = parameters.Count;
        if (numberOfParams > 0)
        {
            int i;  //declaring outside for loop, so I can use it for last line
            for (i=0; i < (numberOfParams-1); i++)
            {
                sw.WriteLine(" \t parameter "+parameters[i].Name + "=" + parameters[i].Value + ",");
            }
            sw.WriteLine("\t parameter "+parameters[i].Name + "=" + parameters[i].Value);
        }
        sw.WriteLine(")("); //this is only for module declaration
        //when reusing for instantiation, instance name goes between the brackets!
    }

    static void AddPorts(string outputPath, List<Port> ports)
    {
        //this method shall add the I/O to the top module
        //It assumes that the previous steps of the module declaration have been added before
        
        using StreamWriter sw = File.AppendText(outputPath);
        int numberOfPorts = ports.Count;
        int portsDone = 0;
        if (numberOfPorts == 0)
        {
            Console.WriteLine("No ports set for top module!");
            return;
        }
        
        //This just writes out the ports in the order they're stored in Topmodule.Ports
        //TODO: Maybe add sorting of ports by direction before writing?
        foreach (Port port in ports)
        {
            //This ensures the comma is left out for the last port declaration
            string lineEnd = portsDone == numberOfPorts - 1 ? "" : ",";
            
            switch (port.Direction)
            {
                case PortDirections.input:
                    if (port.Width == "1")
                    {
                        sw.WriteLine(" \tinput\t\t\t" + port.Name + "{0}", lineEnd);
                    }
                    else
                    {
                        sw.WriteLine(" \tinput  " + port.Width + "\t" + port.Name+ "{0}",  lineEnd);
                    }
                    break;
                case PortDirections.output:
                    if (port.Width == "1")
                    {
                        sw.WriteLine(" \toutput\t\t\t" + port.Name + "{0}",  lineEnd);
                    }
                    else
                    {
                        sw.WriteLine(" \toutput " + port.Width + "\t" + port.Name+ "{0}", lineEnd);
                    }
                    break;
                case PortDirections.inout:
                    if (port.Width == "1")
                    {
                        sw.WriteLine(" \tinout\t\t\t" + port.Name + "{0}",  lineEnd);
                    }
                    else
                    {
                        sw.WriteLine(" \tinout  " + port.Width + "\t" + port.Name+ "{0}", lineEnd);
                    }
                    break;
                default:
                    //TODO: Add proper error handling?
                    Console.WriteLine("Port with no direction? At this time of year, " +
                                      "at this time of day, " +
                                      "in this part of the country," +
                                      "localized entirely within your top module definition!?");
                    return;
            }
            portsDone++;
        }
        sw.WriteLine(");");
    }

    static void AddWires(string outputPath, List<Wire> conWires)
    {
        using StreamWriter sw = File.AppendText(outputPath);
        sw.WriteLine("\n//Automatically generated wire declarations");
        foreach (Wire wire in conWires)
        {
            if (wire.IsTop == false)
            {
                if (wire.Size == 1) sw.WriteLine("wire\t" + wire.Name + ";");
                else sw.WriteLine("wire\t[" + (wire.Size-1) + ":0]\t" + wire.Name + ";");    
            }
        } 
    }

    private void AddModuleInstance(string outputPath, Instance currentInstance)
    {
        //this method adds a module instance to the output file
        using StreamWriter sw = File.AppendText(outputPath);
        int index = 0;  
        
        //style of instantiation depends on whether there are parameters
        if (currentInstance.InstanceParams() != null)
        {
            sw.WriteLine("\n" + currentInstance.ModuleName() + " #(");
            //do parameters stuff
            foreach (Parameter currentParam in currentInstance.InstanceParams()!)
            {
                index++;
                string lineEnd = index == currentInstance.InstanceParams()!.Count ? "" : ",";
                sw.WriteLine("\t.{0}({1}){2}" , currentParam.Name, currentParam.Value, lineEnd);
            }
            sw.WriteLine(") {0} (", currentInstance.InstanceName());           
        }
        else
        {
            sw.WriteLine("\n" + currentInstance.ModuleName() + " " + currentInstance.InstanceName() + "(");   
        }
        sw.Close();
        
        index = 0;    //reusing this variable
        foreach (InstPort port in currentInstance.GetPorts())
        {
            index++;
            bool isLast = (index == currentInstance.GetPorts().Count);
            AddPortSimple(outputPath, port, isLast);
        }
    }

    static void AddPortSimple(string outputPath, InstPort instPort, bool isLast)
    {
        using StreamWriter sw = File.AppendText(outputPath);
        
        string lineEnd = isLast ? "\n);\n" : ",\n"; 
        
        sw.Write("\t.{0}({1}){2}", instPort.PortName, instPort.PinAtIndex(0).ConnectWire, lineEnd);
    }
    /*
     * The following methods are relics from a slightly different concept, that was supposed to allow
     * for individual pin assignments. Keeping them around for inspiration until it is decided whether
     * that functionality is desired.
     */
    
    static void AddInstancePort(string outputPath, InstPort instPort, bool portIsVector, bool wireIsVector, bool isLast)
    {
        using StreamWriter sw = File.AppendText(outputPath);
        if (!portIsVector)
        {
            if (!wireIsVector)  //single bit port connected to single bit wire
            {
                sw.Write("\t." + instPort.PortName + "(" + instPort.PinAtIndex(0).ConnectWire + ")");
            }
            else   //single bit port connected to one bit of vector wire
            {
                sw.Write("\t." + instPort.PortName + "(" + instPort.PinAtIndex(0).ConnectWire
                             + "[" + instPort.PinAtIndex(0).ConnectSideIndex + "])");
            }
        }
        else
        {
            if (!wireIsVector)  //this is the trickiest bit, vector port connected to whatever wires
            {
                Console.WriteLine("NO! I CAN'T DO THIS, YET!");
            }
            else  //port and wire are vectors, for now we assume they are the same size
            {
                sw.Write("\t." + instPort.PortName + "(" + instPort.PinAtIndex(0).ConnectWire + ")");
            }
        }

        sw.Write(isLast ? "\n);\n" : ",\n");
    }

    static bool PortIsVector(InstPort port)
    {
        if (port.PortSize > 1) {return true;}
        else {return false;}
    }

    static bool WireIsVector(InstPort port, List<Wire> wires)
    {
        //Find names of connected wires
        //search list of wires for names and check size of those wires
        string wireName;
        foreach (var pin in port.AllPins())
        {
            if (pin.ConnectWire != null)
            {
                wireName = pin.ConnectWire; //this may justify some additional error handling, but not now
                if (wires.Find(wire => wire.Name == wireName).Size > 1)
                {
                    return true;
                } 
            }
            else
            {
                Console.WriteLine("But my lord, there is no such wire!");
            }
        }
        return false;
    }
}