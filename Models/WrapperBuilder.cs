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
        string wrapperName = configFile.GetSOCName() + "_system_wrapper"; //using SOC Design name + suffix for now
        
        //For now, only some connections to the outside can be inferred
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
        
        //now check for non-submodule ports
        foreach (ConfigItem item in configFile.CoreItems)
        {
            //TODO: Find a way to do this more elegantly, ideally with the info stored in a separate file
            if (item is { Name: "no_uart", Value: "False" })
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
            
            //simple peripherals
            if ((item is { Name: "soft_i2c", Value: "True" } or { Name: "hard_i2c", Value: "True" }))
            {
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.inout,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = item.Name +"_i2cmaster_sda",
                    Signed = false
                });
                wireList.Add(new Wire (item.Name+"_i2cmaster_sda", 1, true, false));
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.inout,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = item.Name + "_i2cmaster_scl",
                    Signed = false
                });
                wireList.Add(new Wire (item.Name + "_i2cmaster_scl", 1, true, false));
            }
            
            if (item is { Name: "soft_spi", Value: "True" } or { Name: "hard_spi", Value: "True" })
            {
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = item.Name + "_master_clk",
                    Signed = false
                });
                wireList.Add(new Wire (item.Name + "_master_clk", 1, true, false));
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = item.Name + "_master_cs_n",
                    Signed = false
                });
                wireList.Add(new Wire (item.Name + "_master_cs_n", 1, true, false));
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.input,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = item.Name + "_master_miso",
                    Signed = false
                });
                wireList.Add(new Wire (item.Name + "_master_miso", 1, true, false));
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = item.Name + "_master_mosi",
                    Signed = false
                });
                wireList.Add(new Wire (item.Name + "_master_mosi", 1, true, false));
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
            /*bus interfaces can be automatically connected to top level with this
             but unfortunately I realized a little too late that's not very useful
            if (item is { Name: "external_bus_master_interface", Value: "True" })
            {
                //first, check for bus specification
                //string busStandard = "wishbone"; !!! currently only wishbone is supported !!!       
                int busDataWidth = 32;      //initialize with default value
                int busAddressWidth = 32;
                int selectWidth = 8;
                foreach (ConfigItem key in configFile.items)
                {
                    switch (key.Name)
                    {
                        //if  (key.Name == "bus_standard"){}    dummy for bus_standard
                        case "bus_data_width":
                            busDataWidth = int.Parse(key.Value);
                            break;
                        case "bus_address_width":
                            busAddressWidth = int.Parse(key.Value);
                            break;
                    }
                }

                selectWidth = (busDataWidth / 8)-1; 
                //Note: LiteX uses Python's floor division (sel_width = data_width // 8)
                //With positive numbers, integer division works the same
                
                //create string for verilog size info in the shape "[width-1 : 0]"
                busDataWidth -= 1;  
                busAddressWidth -= 1;
                string dataWidth = "[" + busDataWidth.ToString() + ":0]";
                string addressWidth = "[" + busAddressWidth.ToString() + ":0]";
                string selWidth = "[" + selectWidth.ToString() + ":0]";
                //restore proper integer values for wire declaration later
                selectWidth += 1;
                busDataWidth += 1;
                busAddressWidth += 1;
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.input,
                    Type = PortTypes.none,
                    Width = "1",
                    Name = "mmap_bus_m_ack",
                    Signed = false
                });
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = addressWidth,
                    Name = "mmap_bus_m_adr",
                    Signed = false
                });
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "[1:0]",    //width of bte is fixed at 2
                    Name = "mmap_bus_m_bte",
                    Signed = false
                });
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "[2:0]",    //width of cti is fixed at 3
                    Name = "mmap_bus_m_cti",
                    Signed = false
                });
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "1",    
                    Name = "mmap_bus_m_cyc",
                    Signed = false
                });
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.input,
                    Type = PortTypes.none,
                    Width = dataWidth,    
                    Name = "mmap_bus_m_dat_r",
                    Signed = false
                });
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = dataWidth,    
                    Name = "mmap_bus_m_dat_w",
                    Signed = false
                });
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.input,
                    Type = PortTypes.none,
                    Width = "1",    
                    Name = "mmap_bus_m_err",
                    Signed = false
                });
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = selWidth,    
                    Name = "mmap_bus_m_sel",
                    Signed = false
                });
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "1",    
                    Name = "mmap_bus_m_stb",
                    Signed = false
                });
                
                wrapperPorts.Add(new Port
                {
                    Direction = PortDirections.output,
                    Type = PortTypes.none,
                    Width = "1",    
                    Name = "mmap_bus_m_we",
                    Signed = false
                });
                
                wireList.Add(new Wire ("mmap_bus_m_ack", 1, true, true));
                wireList.Add(new Wire ("mmap_bus_m_adr", busAddressWidth , true, false));
                wireList.Add(new Wire ("mmap_bus_m_bte", 2 , true, false));
                wireList.Add(new Wire ("mmap_bus_m_cti", 3 , true, false));
                wireList.Add(new Wire ("mmap_bus_m_cyc", 1 , true, false));
                wireList.Add(new Wire ("mmap_bus_m_dat_r", busDataWidth, true, true));
                wireList.Add(new Wire ("mmap_bus_m_dat_w", busDataWidth, true, false));
                wireList.Add(new Wire ("mmap_bus_m_err", 1, true, true));
                wireList.Add(new Wire ("mmap_bus_m_sel", selectWidth, true, false));
                wireList.Add(new Wire ("mmap_bus_m_stb", 1 , true, false));
                wireList.Add(new Wire ("mmap_bus_m_we", 1 , true, false));
            }
            
            if (item is { Name: "external_bus_slave_interface", Value: "True" })
            {
                //analogous to "external_master_interface"
                //but probably both are useless in most cases because this is only for the wrapper ports and
                //usually this bus doesn't go outside the FPGA...
            }
            */
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
        
        foreach (ConfigItem item in configFile.CoreItems)
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
        foreach (ConfigItem item in configFile.CoreItems)
        {
            /*
             * If the base SoC has a bus interface, make wires to connect to it.
             * I am currently unsure how to handle the connection to of other instances
             * to the bus. The easiest solution would be to just assume that every bus
             * interface is meant to connect to the SoC Bus, but I don't really like it.
             * Probably because it's bad.
             *
             * Currently, what this does is just create wires and connect them on the SoC
             * side. 
             */
            //first, check for bus specification
            //string busStandard = "wishbone"; !!! currently only wishbone is supported !!!       
            int busDataWidth = 32;      //initialize with default value
            int busAddressWidth = 32;
            int selectWidth = 8;
            foreach (ConfigItem key in configFile.CoreItems)
            {
                switch (key.Name)
                {
                    //if  (key.Name == "bus_standard"){}    dummy for bus_standard
                    case "bus_data_width":
                        busDataWidth = int.Parse(key.Value);
                        break;
                    case "bus_address_width":
                        busAddressWidth = int.Parse(key.Value);
                        break;
                }
            }

            selectWidth = (busDataWidth / 8); 
            //Note: LiteX uses Python's floor division (sel_width = data_width // 8)
            //With positive numbers, integer division works the same
            
            if (item is { Name: "external_bus_master_interface", Value: "True" })
            {
                wireList.Add(new Wire ("mmap_bus_m_ack", 1, false, true));
                wireList.Add(new Wire ("mmap_bus_m_adr", busAddressWidth , false, false));
                wireList.Add(new Wire ("mmap_bus_m_bte", 2 , false, false));
                wireList.Add(new Wire ("mmap_bus_m_cti", 3 , false, false));
                wireList.Add(new Wire ("mmap_bus_m_cyc", 1 , false, false));
                wireList.Add(new Wire ("mmap_bus_m_dat_r", busDataWidth, false, true));
                wireList.Add(new Wire ("mmap_bus_m_dat_w", busDataWidth, false, false));
                wireList.Add(new Wire ("mmap_bus_m_err", 1, false, true));
                wireList.Add(new Wire ("mmap_bus_m_sel", selectWidth, false, false));
                wireList.Add(new Wire ("mmap_bus_m_stb", 1 , false, false));
                wireList.Add(new Wire ("mmap_bus_m_we", 1 , false, false));
            }

            if (item is { Name: "external_bus_slave_interface", Value: "True" })
            {
                wireList.Add(new Wire ("mmap_bus_s_ack", 1, false, false));
                wireList.Add(new Wire ("mmap_bus_s_adr", busAddressWidth , false, true));
                wireList.Add(new Wire ("mmap_bus_s_bte", 2 , false, true));
                wireList.Add(new Wire ("mmap_bus_s_cti", 3 , false, true));
                wireList.Add(new Wire ("mmap_bus_s_cyc", 1 , false, true));
                wireList.Add(new Wire ("mmap_bus_s_dat_r", busDataWidth, false, false));
                wireList.Add(new Wire ("mmap_bus_s_dat_w", busDataWidth, false, true));
                wireList.Add(new Wire ("mmap_bus_s_err", 1, false, false));
                wireList.Add(new Wire ("mmap_bus_s_sel", selectWidth, false, true));
                wireList.Add(new Wire ("mmap_bus_s_stb", 1 , false, true));
                wireList.Add(new Wire ("mmap_bus_s_we", 1 , false, true));
            } 
        }    
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