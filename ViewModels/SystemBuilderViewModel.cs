using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using systembuilderGUI.ViewModels;

namespace systembuilderGUI.ViewModels;

public class SystemBuilderViewModel : ExtendedTool
{
    public const string IconKey = "VsImageLib.ToolsDefault16X";

    public SystemBuilderContentViewModel Content { get; } = new();

    public SystemBuilderViewModel() : base(IconKey)
    {
        Id = "SystemBuilder";
        Title = "Irrelevant Title";
    }

    public override void InitializeContent()
    {
        //var activeProjectName = ContainerLocator.Current.Resolve<IProjectExplorerService>().ActiveProject.Name;
        Title = "SystemBuilder";
    }
}
