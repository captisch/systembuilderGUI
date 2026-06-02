using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using systembuilderGUI.ViewModels;

namespace systembuilderGUI.Views;

public partial class SystemBuilderView : UserControl
{
    public SystemBuilderView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is SystemBuilderViewModel vm)
            vm.Content.StorageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
    }
}
