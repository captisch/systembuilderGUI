using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace systembuilderGUI.Models;

public class WrapperBuilder
{
    private ConfigFile configFile;
    private List<Instance> instanceList = new List<Instance>();
    private List<Wire> wireList = new List<Wire>();
    private Module? wrapperDefinition;
    
    /*
     * This class contains the methods to prepare the input of the input of the VerilogGenerator.
     * It must only be run after generating the SoC because the instance of the main SoC is generated
     * by reading the module definition of the generated Verilog module.
     */

    public WrapperBuilder(ConfigFile configFile)
    {
        this.configFile = configFile;
    }
    
    public void GenerateWrapper(string outputPath)
    {
        //NOTE: Methods must be called in this order to work properly! 
        MakeInstances();
        MakeWrapperDefinition();
        MakeWiring();
        VerilogGenerator generator = new VerilogGenerator(instanceList, wrapperDefinition, wireList);
        generator.GenerateVerilog(outputPath);
    }

    private void MakeWrapperDefinition()
    {
        /* This is used to create the top module definition for the wrapper.
         * It needs a name and port definitions but no parameters or logic.
         */
        
        //Start by giving it a name
        string wrapperName = "system_wrapper"; //using fixed name for now
        
        //For now, only some connections to the outside can be inferred
        //"clk", "rst" and if available "uart_rx", "uart_tx" will be created for wrapper
        //and connected to the main SoC module
        List<Port> wrapperPorts =
        [
            new Port
            {
                Direction = PortDirections.input,
                Type = PortTypes.none,
                Width = "1",
                Name = "clk",
                Signed = false
            },

            new Port
            {
                Direction = PortDirections.input,
                Type = PortTypes.none,
                Width = "1",
                Name = "rst",
                Signed = false
            }
        ];
        
        //Adding "virtual" wires for the wrapper ports
        wireList.Add(new Wire ("clk", 1, true, true));
        wireList.Add(new Wire ("rst", 1, true, true));

        //now check for uart ports (same routine can be used for other SoC module ports)
        foreach (ConfigItem item in configFile.items)
        {
            if (item is { Name: "no_uart", Value: "False" })    //note: False must be capitalized!
            {
                //Debug:
                //Console.WriteLine("Found uart!");
                
                //This means the main SoC has a uart and we should make ports for that
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.input,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "uart_rx",
                    Signed = false
                });
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "uart_tx",
                    Signed = false
                });
                wireList.Add(new Wire ("uart_rx", 1, true, true));
                wireList.Add(new Wire ("uart_tx", 1, true, false));
            }

            if (item is { Name: "soft_i2c", Value: "True" })
            {
                //NOTE: This does not work correctly if soft and hard i2c are present simultaneously!
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.inout,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "i2cmaster_sda",
                    Signed = false
                });
                wireList.Add(new Wire ("i2cmaster_sda", 1, true, false));
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.inout,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "i2cmaster_scl",
                    Signed = false
                });
                wireList.Add(new Wire ("i2cmaster_scl", 1, true, false));
            }
            if (item is { Name: "hard_i2c", Value: "True" })
            {
                //NOTE: This does not work correctly if soft and hard i2c are present simultaneously!
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.inout,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "i2cmaster_sda",
                    Signed = false
                });
                wireList.Add(new Wire ("i2cmaster_sda", 1, true, false));
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.inout,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "i2cmaster_scl",
                    Signed = false
                });
                wireList.Add(new Wire ("i2cmaster_scl", 1, true, false));
            }
            if (item is { Name: "soft_spi", Value: "True" })
            {
                //NOTE: This does not work correctly if soft and hard spi are present simultaneously!
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "spimaster_clk",
                    Signed = false
                });
                wireList.Add(new Wire ("spimaster_clk", 1, true, false));
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "spimaster_cs_n",
                    Signed = false
                });
                wireList.Add(new Wire ("spimaster_cs_n", 1, true, false));
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.input,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "spimaster_miso",
                    Signed = false
                });
                wireList.Add(new Wire ("spimaster_miso", 1, true, false));
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "spimaster_mosi",
                    Signed = false
                });
                wireList.Add(new Wire ("spimaster_mosi", 1, true, false));
            }
            if (item is { Name: "hard_spi", Value: "True" })
            {
                //NOTE: This does not work correctly if soft and hard i2c are present simultaneously!
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "spimaster_clk",
                    Signed = false
                });
                wireList.Add(new Wire ("spimaster_clk", 1, true, false));
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "spimaster_cs_n",
                    Signed = false
                });
                wireList.Add(new Wire ("spimaster_cs_n", 1, true, false));
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.input,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "spimaster_miso",
                    Signed = false
                });
                wireList.Add(new Wire ("spimaster_miso", 1, true, false));
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "spimaster_mosi",
                    Signed = false
                });
                wireList.Add(new Wire ("spimaster_mosi", 1, true, false));
            }
            if (item is { Name: "SDcard", Value: "True" })
            {
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.input,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "sdcard_cd",
                    Signed = false
                });
                wireList.Add(new Wire ("sdcard_cd", 1, true, true));
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "sdcard_clk",
                    Signed = false
                });
                wireList.Add(new Wire ("sdcard_clk", 1, true, false));
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.inout,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "sdcard_cmd",
                    Signed = false
                });
                wireList.Add(new Wire ("sdcard_cmd", 1, true, false));
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.inout,
                    Type = PortTypes.none,
                    Width = "[3:0]",
                    Name = "sdcard_data",
                    Signed = false
                });
                wireList.Add(new Wire ("sdcard_data", 4, true, false));
            }
        }
        //Check the listed instances for connections to Wrapper
        foreach (Instance instance in instanceList)
        {
            if (instance.InstanceName() == "main_soc")
            {
                //skip the main_soc for now
                //maybe useful later
            }
            else
            {
                foreach (InstPort port in instance.GetPorts())
                {
                    if (port.ConnectToWrapper)
                    {
                        string width;
                        if (port.PortSize == 1) width = "1";
                        else width = "[" + (port.PortSize-1) + ":0]";
                    
                        string name = instance.InstanceName() + "_" + port.PortName;
                        wrapperPorts.Add(new Port
                        {
                            Direction = port.PortDirection,
                            Type = PortTypes.none,
                            Width = width,
                            Name = name,
                            Signed = false
                        });
                        bool hasDriver = (port.PortDirection == PortDirections.output);
                        Wire tempWire = new Wire(name, port.PortSize, true, hasDriver);
                        wireList.Add(tempWire);
                        instance.SetConnection(port.PortName, tempWire);
                    }
                }
            }
            
        }
        wrapperDefinition = new Module
        {
            Name = wrapperName,
            Ports = wrapperPorts
        };
    }

    private void MakeInstances()
    {
        //Create the list of Instances from the listed Submodules in the config
        foreach (SubModule subMod in configFile.subModules)
        {
            //internal submodules aren't relevant here
            if (subMod.IsExternalModule)
            {
                instanceList.Add(new Instance(subMod));   
            }
        }
        
        //Then add the SoC module created by LiteX
        VerilogParser parser = new VerilogParser();
        var socName = "new_soc_design";   //initialize with default name
        
        foreach (ConfigItem item in configFile.items)
        {
            if (item.Name == "name")
            {
                //Debug:
                Console.WriteLine("found SoC name!");
                socName = item.Value;
                break;
            }
        }
        var socFilePath = Path.Combine(configFile.OutputDirPath, socName, "gateware", socName + ".v" );
        
        List<Module> modulesTemp = parser.ReadVerilog(socFilePath);
         
        foreach (Module module in modulesTemp)
        {
            //Debug:
            Console.WriteLine("Found module {0} in SoC file" ,module.Name);
            
            if (module.Name == socName)
            {
                SubModule tempSubMod = new SubModule()
                {
                    Module = module,
                    Source = null, //not used here, maybe in the future?
                    Filename = Path.GetFileName(socFilePath),
                    Instance = "main_soc",
                }; 
                Instance soc = new Instance(tempSubMod);
                instanceList.Add(soc);
            }
        }
    }

    private void MakeWiring()
    {
        //This creates the wiring between the module instances
        //MakeTopModule and MakeInstances must be run before this.
        foreach (Instance instance in instanceList)
        {
            if (instance.InstanceName() != "main_soc")
            {
                //Routine for every instance but the main SoC
                foreach (InstPort port in instance.GetPorts())
                {
                    if (!port.ConnectToSoC) continue;
                    string wireName = instance.InstanceName() + "_" + port.PortName;
                    Wire tempWire = new Wire(wireName, port.PortSize, false, false);
                    wireList.Add(tempWire);
                    instance.SetConnection(port.PortName, tempWire);
                }    
            }
            else
            {
                //Routine for main_soc
                //WARNING! THIS ASSUMES main_soc IS ALWAYS LAST IN instanceList (see "MakeInstances" method)
                foreach (InstPort port in instance.GetPorts())
                {
                    foreach (Wire wire in wireList)
                    {
                        if (wire.Name == port.PortName)
                        {
                            instance.SetConnection(port.PortName, wire);
                        }
                    }
                }
            }
        }
    }
}