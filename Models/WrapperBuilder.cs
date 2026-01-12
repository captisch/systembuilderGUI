using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace systembuilderGUI.Models;

public class WrapperBuilder
{
    private ConfigFile configFile;
    private List<Instance> instanceList = new List<Instance>();
    private List<Wire> wireList = new List<Wire>();
    private Module wrapperDefinition;
    
    /*
     * This class contains the methods to prepare the input of the input of the VerilogGenerator.
     * It must only be run after generating the SoC because the instance of the main SoC is generated
     * by reading the module definition of the generated Verilog module.
     */

    public WrapperBuilder(ConfigFile configFile)
    {
        this.configFile = configFile;
    }
    
    public void GenerateWrapper(string path)
    {
        MakeWrapperDefinition();
        MakeInstances();
        MakeWiring();
        VerilogGenerator generator = new VerilogGenerator(instanceList, wrapperDefinition, wireList);
        generator.GenerateVerilog(path);
    }

    private void MakeWrapperDefinition()
    {
        /* This is used to create the top module definition for the wrapper.
         * It needs a name and port definitions but no parameters or logic.
         * Currently, not all information is available in the SystemBuilder to determine
         * all connections from the wrapper to other modules. This may be added in 
         * later versions.
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
            if (item is { Name: "no_uart", Value: "false" })
            {
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
        string socName = "new_soc_design";   //initialize with default name
        string socFilePath = @"C:\fentwumsGUI\systembuilderOutput\";    
        /*
         * socFilePath may not work entirely as intended, but let's see for now
        */
        
        foreach (ConfigItem item in configFile.items)
        {
            if (item.Name == "name")
            {
                socName = item.Value;
                break;
            }
        }
        
        List<Module> modulesTemp = parser.ReadVerilog(socFilePath);
         
        foreach (Module module in modulesTemp)
        {
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