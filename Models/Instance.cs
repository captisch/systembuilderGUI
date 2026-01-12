using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia.Controls.Converters;

namespace systembuilderGUI.Models;

public struct Pin
{
    public string? ConnectWire { get; set; }
    public int? ConnectSideIndex { get; set; }
}

public struct InstPort
{
    private string portName;
    private int portSize;
    private PortDirections portDirection; 
    private Pin[] portPins;
    private bool connectToSoC;

    public InstPort(string portName, int portSize, PortDirections portDirection, bool connectSoC)
    {
        this.portName = portName;
        this.portSize = portSize;
        this.portDirection = portDirection;
        portPins = new Pin[this.portSize];
        connectToSoC = connectSoC;
    }
    
    public string PortName => portName;
    public PortDirections PortDirection => portDirection;
    public int PortSize => portSize;
    public bool ConnectToSoC => connectToSoC;
    public void SetConnect(string connectWire, int connectSideIndex, int portSideIndex)
    {
        //TODO: See if check for multiple drivers is relevant here?
        Pin[] pins = this.portPins;
        portPins[portSideIndex].ConnectWire = connectWire;
        portPins[portSideIndex].ConnectSideIndex = connectSideIndex;
        this.portPins = pins;
    }

    public Pin PinAtIndex(int pinIndex)
    {
        return portPins[pinIndex];
    }

    public Pin[] AllPins()
    {
        return portPins;
    }
}

public class Instance
{
    private string _moduleName;
    private string _instanceName;
    private List<Parameter>? _instanceParams;
    private List<InstPort> _ports;
    //Getters
    public List<InstPort> GetPorts()
    {
        return _ports;
    }

    public string ModuleName()
    {
        return _moduleName;
    }

    public string InstanceName()
    {
        return _instanceName;
    }

    public List<Parameter>? InstanceParams()
    {
        return _instanceParams;
    }

    public int GetPortSize(string portName)
    {
        int index = _ports.FindIndex(x => x.PortName == portName);
        if (index != -1)
        {
            return _ports[index].PortSize;
        }
        return 0;
    }
    
    //Constructor
    public Instance(SubModule baseModule)
    {
        _moduleName = baseModule.Module.Name;
        _instanceName = baseModule.Instance;
        _instanceParams = baseModule.Module.Parameters;
        _ports = PortSetup(baseModule.Module.Ports);
         
        /*Debug Code:.............................................................
        //TODO: Adapt to new structure if required
        Console.WriteLine("Based on module " + baseModule.Name + " stored as " + _moduleName + " with ports:");
        Console.WriteLine("Inputs:");
        foreach (var port in baseModule.Inputs)
        {
            Console.WriteLine(port.Name + " of size " + port.Size);
        }
        Console.WriteLine("Outputs:");
        foreach (var port in baseModule.Outputs)
        {
            Console.WriteLine(port.Name +" of size " + port.Size);
        }
        Console.WriteLine("Tri States:");
        foreach (var port in baseModule.TriStates)
        {
            Console.WriteLine(port.Name + " of size " + port.Size);
        }
        
        Console.WriteLine("Created Instance named: " + _instanceName);
        Console.WriteLine("With the following Ports:");
        foreach (var port in _inputPorts)
        {
            Console.WriteLine(port.PortName + " with " + port.PortSize + " Pins");
        }
        foreach (var port in _outputPorts)
        {
            Console.WriteLine(port.PortName + " with " + port.PortSize + " Pins");
        }
        foreach (var port in _triStatePorts)
        {
            Console.WriteLine(port.PortName + " with " + port.PortSize + " Pins");
        }
        End of Debug Code............................................................*/
    }
    
    //miscellaneous methods
    /*TODO: Very important!
      Check if using the Wire objects directly like this works as intended. The idea is
      to set hasDriver to true, once an "output" type is connected
      I'm currently unsure how to handle Tristate (inout) types, but they should not have
      the driver issue anyway, at least as long as they're connected properly...    
    */

    public void SetConnection(string portName, Wire conWire)
    {
        /*
         * NOTE: This method is currently only used for wrapper creation where other
         * parts of the software should ensure that no driver conflicts exist.
         * If there are plans to use this in other contexts, a check (using conWire.HasDriver)
         * may be sensible to do here.
         */
        int portIndex = FindPort(portName);
        if (portIndex != -1)
        {
            var port = _ports[portIndex];

            for (int i = 0; i < port.PortSize; i++)
            {
                port.SetConnect(conWire.Name, i, i); 
            }
            _ports.RemoveAt(portIndex);
            _ports.Insert(portIndex, port);
        }
    }
    
    /*
     This method is currently an unused relic kept around for potential reactivation later on.
     It can be used to connect individual Bits of ports and wires, if that is required.
     
     public void SetConnectionByIndex(string portName, Wire conWire, int pinIndex, int wireIndex)
    {      
        //this methods assumes unique port names and does not check for illegal connections
        int portIndex = FindPort(portName);
        
        Debug Code
        Console.WriteLine("looking for pin {0} of port {1}", pinIndex, portName);
        Console.WriteLine("Index in list of inputs:" + portIndex);
        //End of Debug Code
        
        if (portIndex != -1)
        {
            var port = _ports[portIndex];
            
            //Check if wire is already connected to other output (signal driver)
            if (port.PortDirection == PortDirections.output)
            {
                if (conWire.HasDriver[wireIndex] == true)
                {
                    Console.WriteLine("ERROR: Index {0} of {1} already has driver!", wireIndex, conWire.Name);
                    return;
                }
                else
                {
                    conWire.HasDriver[wireIndex] = true;
                }
            }
            port.SetConnect(conWire.Name, wireIndex, pinIndex);
            _ports.RemoveAt(portIndex);
            _ports.Insert(portIndex, port);
            return;
        }
        //In case Pin can't be found:
        Console.WriteLine("ERROR! Couldn't find requested Pin: " + portName);
    }*/

    public void SetParameter(string parameterName, string value)
    {
        int parameterIndex = _instanceParams.FindIndex(x => x.Name == parameterName);
        if (parameterIndex != -1)
        {
            var instanceParam = _instanceParams[parameterIndex];
            instanceParam.Value = value;
            _instanceParams.RemoveAt(parameterIndex);
            _instanceParams.Insert(parameterIndex, instanceParam);
            return;
        }
        Console.WriteLine("ERROR! Parameter doesn't exist!");
    }
    
    private List<InstPort> PortSetup(List<Port> modulePorts)
    {
        List<InstPort> ports = new List<InstPort>();
        foreach (Port modPort in modulePorts)
        {
            //convert "Width" string to integer:
            string tempWidth = modPort.Width;
            int widthNum = 0;
            //First step is to check if it's just "1"
            if (tempWidth == "1")
            {
                widthNum = 1;    
            }
            else
            {
                char[] separators = ['[', ']', ':'];
                string[] substrings = tempWidth.Split(separators, StringSplitOptions.RemoveEmptyEntries);

                if (substrings.Length != 2)
                {
                    //For now this is just some debug messages and a null return
                    //Long-term it would probably be good to have actual error handling...
                    Console.WriteLine("ERROR: Invalid width format! There should be only 2 substrings.");
                    Console.WriteLine("These substrings have been identified:");
                    foreach (string sub in substrings)
                    {
                        Console.WriteLine(sub);
                    }

                    return null;
                }
                else
                {
                    //Again, we begin with the easy option (e.g. [7:0])
                    substrings[0] = substrings[0].Trim(); //remove white spaces
                    substrings[1] = substrings[1].Trim();
                    int tempNum = 0;
                    bool isNumber = int.TryParse(substrings[0], out tempNum);
                    if (isNumber)
                    {
                        widthNum = tempNum + 1;
                        isNumber = int.TryParse(substrings[1], out tempNum);
                        if (isNumber)
                        {
                            widthNum -= tempNum;
                        }
                        else
                        {
                            //I think it's not unreasonable to treat this unlikely case as an error for now
                            Console.WriteLine("ERROR! I don't know what you're trying here but this ain't it.");
                            return null;
                        }
                    }
                    else
                    {
                        int index = 0;
                        List<string> tokens = new List<string>();
                        string temp = "";
                        var builder = new StringBuilder();
                        bool spaceFound = false;

                        while (index < substrings[0].Length)
                        {
                            //maybe I can rework this part so I check for proper syntax
                            //i.e. value,op,value,op
                            //if op,op appears throw error
                            //value, value can't occur, value can only be invalid 
                            switch (substrings[0][index])
                            {
                                case '-':
                                //break;
                                case '+':
                                //break;
                                case '*':
                                //break;
                                case '/':
                                case '(':
                                //break;
                                case ')':
                                    spaceFound = false;
                                    if (builder.Length > 0)
                                    {
                                        //Save previous characters to token an reset StringBuilder
                                        temp = builder.ToString();
                                        tokens.Add(temp);
                                        builder.Clear();
                                        Console.WriteLine("Saving Operand: {0}", temp);
                                    }

                                    //Saving current operator
                                    temp = substrings[0][index].ToString();
                                    tokens.Add(temp);

                                    //Debug Code:
                                    Console.WriteLine("Saving Operator: {0}", temp);
                                    break;
                                case ' ':
                                    //this filters spaces and helps detect invalid syntax
                                    if (builder.Length > 0)
                                    {
                                        spaceFound = true;
                                        //Save previous characters to token collection and reset StringBuilder
                                        temp = builder.ToString();
                                        tokens.Add(temp);
                                        builder.Clear();
                                    }

                                    break;
                                default:
                                    if (spaceFound == true)
                                    {
                                        /*If a space was found after valid chars, the next char must be
                                         * space or operator. Otherwise it's invalid syntax.
                                         * I think even this check might be overdoing it, as we expect
                                         * valid Verilog as input.
                                         */
                                        Console.WriteLine("ERROR! Invalid Syntax. Token can't contain spaces.");
                                        return null;
                                    }
                                    else
                                    {
                                        Console.WriteLine(index);
                                        builder.Append(substrings[0][index]);
                                    }

                                    break;
                            }

                            index++;
                        }

                        //Save last token to collection
                        if (builder.Length > 0)
                        {
                            temp = builder.ToString();
                            tokens.Add(temp);
                        }

                        //This should give me a "tokenized" version of the first part of the [x:y] expression
                        //with all spaces removed.
                        //Now I can reorder them to postfix, then do the numbers

                        Stack<string> opStack = new Stack<string>();
                        List<string> postfix = new List<string>();

                        static int prec(string c)
                        {
                            if (c == "/" || c == "*")
                                return 2;
                            else if (c == "+" || c == "-")
                                return 1;
                            else
                                return -1;
                        }

                        //reset variables for reuse here
                        tempNum = 0;
                        isNumber = false;
                        //Debug Message
                        Console.WriteLine("Listing tokens:");
                        foreach (string token in tokens)
                        {
                            //Debug Message
                            Console.WriteLine(token);

                            //length 1 is probably an operator so we check that first
                            switch (token)
                            {
                                case "+":
                                //break;
                                case "-":
                                //break;
                                case "*":
                                //break;
                                case "/":
                                    while (opStack.Count > 0 && opStack.Peek() != "(" &&
                                           prec(opStack.Peek()) >= prec(token))
                                    {
                                        postfix.Add(opStack.Pop());
                                    }

                                    opStack.Push(token);
                                    break;
                                case "(":
                                    opStack.Push("(");
                                    break;
                                case ")":
                                    while (opStack.Count > 0 && opStack.Peek() != "(")
                                    {
                                        postfix.Add(opStack.Pop());
                                    }

                                    opStack.Pop(); //remove "(" from stack
                                    break;
                                default:
                                    postfix.Add(token);
                                    break;
                            }
                        }

                        //Pop remaining operators
                        while (opStack.Count > 0)
                        {
                            postfix.Add(opStack.Pop());
                        }

                        //now I need a postfix calculator that also handles the parameter conversion...
                        Stack<int> valueStack = new Stack<int>();

                        foreach (string element in postfix)
                        {
                            //Debug Message
                            Console.WriteLine(element);

                            switch (element)
                            {
                                case "+":
                                    //TODO: maybe add a little error handling here?
                                    tempNum = valueStack.Pop() + valueStack.Pop();
                                    valueStack.Push(tempNum);
                                    break;
                                case "-":
                                    //Not sure if I'm getting the order of operands correctly here...
                                    tempNum = valueStack.Pop();
                                    valueStack.Push(valueStack.Pop() - tempNum);
                                    break;
                                case "*":
                                    tempNum = valueStack.Pop() * valueStack.Pop();
                                    valueStack.Push(tempNum);
                                    break;
                                case "/":
                                    tempNum = valueStack.Pop();
                                    valueStack.Push(valueStack.Pop() / tempNum);
                                    break;
                                default:
                                    tempNum = 0;
                                    isNumber = int.TryParse(element, out tempNum);
                                    if (isNumber)
                                    {
                                        valueStack.Push(tempNum);
                                    }
                                    else
                                    {
                                        //Now this is where the parameter values come in...
                                        bool paramFound = false;
                                        foreach (Parameter param in _instanceParams)
                                        {
                                            if (param.Name == element)
                                            {
                                                //TODO: Maybe add some checks and not just assume it will work fine?
                                                tempNum = int.Parse(param.Value);
                                                paramFound = true;
                                                break;
                                            }
                                        }

                                        if (!paramFound)
                                        {
                                            //TODO: Proper error message?
                                            Console.WriteLine("ERROR! Parameter '{0}' not found.", element);
                                        }
                                        else
                                        {
                                            valueStack.Push(tempNum);    
                                        }
                                    }
                                    break;
                            }
                        }

                        widthNum = valueStack.Pop();
                        
                        //Debug Message:
                        int stacksize = valueStack.Count();
                        Console.WriteLine("Stack check! Size is: {0}", stacksize);
                    }
                }
            }

            InstPort currentPort = new InstPort(modPort.Name, widthNum, modPort.Direction, modPort.RouteToTopmodule);
            ports.Add(currentPort);
            //Debug Message:
            Console.WriteLine("Added Port: " + currentPort.PortName + " with " + currentPort.PortSize + " Pins");
        }
        //Debug Message
        Console.WriteLine("Return List contains the following "+ ports.Count + " ports:");
        foreach (var port in ports)
        {
            Console.WriteLine(port.PortName + " with " + port.PortSize + " Pins");
        }
        return ports;
    }

    private int FindPort(string portName)
    {
        return _ports.FindIndex(x => x.PortName == portName);
    }
    
}