using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace systembuilderGUI.Views;

public partial class ExtensionButton : UserControl
{
    public ExtensionButton(ICommand openCommand)
    {
        InitializeComponent();
        SystemBuilderButton.Command = openCommand;
    }
}