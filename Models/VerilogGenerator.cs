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
        string outputPath = targetDirectory + TopModule.Name + ".v";

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
        AddPorts(outputPath, TopModule.Inputs, TopModule.Outputs, TopModule.TriStates);
        AddWires(outputPath, ConWires);
        
        foreach (Instance currentInstance in Instances)
        {
            Console.WriteLine("What happens here?");
            AddModuleInstance(outputPath, currentInstance);
        }
    }
    
    //auxiliary methods for organization
    static void AddParams(string outputPath, List<ModuleParam> parameters)
    {
        using StreamWriter sw = File.AppendText(outputPath);
        //going through all params, last line is different (no ',' at the end)
        int numberOfParams = parameters.Count;
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

    static void AddPorts(string outputPath, List<Port> inputs, List<Port> outputs, List<Port> triStates)
    {
        //this method shall add the I/O to the top module
        //It assumes that the previous steps of the module declaration have been added before
        
        using StreamWriter sw = File.AppendText(outputPath);
        int numberOfInputs = inputs.Count;
        int numberOfOutputs = outputs.Count;
        int numberOfTriStates = triStates.Count;
        int numberOfPorts = numberOfInputs + numberOfOutputs + numberOfTriStates;
        if (numberOfPorts == 0)
        {
            Console.WriteLine("No ports set for top module!");
            return;
        }
        if (numberOfInputs > 0)
        {
            int repetitions = numberOfInputs - 1;   //
            bool lastPortIsInput = false;
            
            if (numberOfInputs < numberOfPorts)
            {
                repetitions += 1;
            }
            else
            {
                lastPortIsInput = true;
                Console.WriteLine("Last port should be input");
            }
            for (int i = 0; i < repetitions; i++)
            {
                if (inputs[i].Size == 1)
                {
                    sw.WriteLine(" \tinput " + inputs[i].Name + ",");
                }
                else
                {
                    sw.WriteLine(" \tinput " + "[" + (inputs[i].Size - 1) +":0] " + inputs[i].Name+ ",");
                }
            }

            if (lastPortIsInput)
            {
                if (inputs[repetitions].Size == 1)
                {
                    sw.WriteLine(" \tinput " + inputs[repetitions].Name);
                }
                else
                {
                    sw.WriteLine(" \tinput " + "[" + (inputs[repetitions].Size - 1) +":0] " + inputs[repetitions].Name);
                    
                }
                sw.WriteLine(");");
                return;
            }
            Console.WriteLine("Reached end of inputs. Last port is not input.");
        }

        if (numberOfOutputs > 0)
        {
            int repetitions = numberOfOutputs - 1;   //
            bool lastPortIsOutput = false;
            
            if (numberOfTriStates > 0)
            {
                repetitions += 1;
            }
            else
            {
                lastPortIsOutput = true; 
                Console.WriteLine("Last port should be output.");
            }
            for (int i = 0; i < repetitions; i++)
            {
                if (outputs[i].Size == 1)
                {
                    sw.WriteLine(" \toutput " + outputs[i].Name + ",");
                }
                else
                {
                    sw.WriteLine(" \toutput " + "[" + (outputs[i].Size - 1) +":0] " + outputs[i].Name+ ",");
                }
            }
            if (lastPortIsOutput)
            {
                Console.WriteLine("Last port is output.");
                if (outputs[repetitions].Size == 1)
                {
                    sw.WriteLine(" \toutput " + outputs[repetitions].Name);
                }
                else
                {
                    sw.WriteLine(" \toutput " + "[" + (outputs[repetitions].Size - 1) +":0] " + outputs[repetitions].Name);
                }
                sw.WriteLine(");");
                return;
            }
        }
        Console.WriteLine("Last port should be tristate.");
        if (numberOfTriStates > 0)
        {
            int repetitions = numberOfTriStates - 1;
            for (int i = 0; i < repetitions; i++)
            {
                if (triStates[i].Size == 1)
                {
                    sw.WriteLine(" \tinout " + triStates[i].Name + ",");
                }
                else
                {
                    sw.WriteLine(" \tinout " + "[" + (triStates[i].Size - 1) +":0] " + triStates[i].Name+ ",");
                }
            }
            if (triStates[repetitions].Size == 1)
            {
                sw.WriteLine(" \tinout " + triStates[repetitions].Name);
            }
            else
            {
                sw.WriteLine(" \tinout " + "[" + (triStates[repetitions].Size - 1) +":0] " + triStates[repetitions].Name);
                
            }
            sw.WriteLine(");");
            return;
        }
        Console.WriteLine("Something went wrong here!");
    }

    static void AddWires(string outputPath, List<Wire> conWires)
    {
        using StreamWriter sw = File.AppendText(outputPath);
        sw.WriteLine("\n//Automatically generated wire declarations");
        foreach (Wire wire in conWires)
        {
            if (wire.IsTop == false)
            {
                sw.WriteLine("wire\t[" + (wire.Size-1) + ":0]\t" + wire.Name+ ";");    
            }
        } 
    }

    private void AddModuleInstance(string outputPath, Instance currentInstance)
    {
        //this method adds a module instance to the output file
        using StreamWriter sw = File.AppendText(outputPath);
        
        List<InstPort> instPorts = new List<InstPort>();
        
        foreach (InstPort inPort in currentInstance.GetInputPorts())
        {
            instPorts.Add(inPort);
        }
        foreach (InstPort outPort in currentInstance.GetOutputPorts())
        {
            instPorts.Add(outPort);
        }
        foreach (InstPort triPort in currentInstance.GetTriStatePorts())
        {
            instPorts.Add(triPort);
        }
        
        //start module instance with module name and instance name
        sw.WriteLine("\n" + currentInstance.ModuleName() + " " + currentInstance.InstanceName() + "(");
        //parameters are not supported in this version
        Console.WriteLine(instPorts.Count);
        int index = instPorts.Count -1; //reusing the variable for number of elements-1
        sw.Close();
        for (int i = 0; i < index; i++)
        { 
            bool portVector = PortIsVector(instPorts[i]);
            bool wireVector = WireIsVector(instPorts[i], ConWires);
            AddInstancePort(outputPath, instPorts[i], portVector, wireVector, false);
        }  
        //Last port gets no comma
        bool pVector = PortIsVector(instPorts[index]);
        bool wVector = WireIsVector(instPorts[index], ConWires);
        AddInstancePort(outputPath, instPorts[index], pVector, wVector, true);
    }

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

        if (isLast)
        {
            sw.Write("\n);\n");
        }
        else
        {
            sw.Write(",\n");
        }
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
                Console.WriteLine("Couldn't find wire with that name. Something went wrong.");
            }
        }
        return false;
    }
}