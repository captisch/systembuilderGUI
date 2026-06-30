using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using systembuilderGUI.ViewModels;
using systembuilderGUI.Views;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace systembuilderGUI;

public class SystembuilderExtensionModule : OneWareModuleBase
{
    
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<SystemBuilderViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IMainDockService>();
        var windowService = serviceProvider.Resolve<IWindowService>();

        dockService.RegisterLayoutExtension<SystemBuilderViewModel>(DockShowLocation.Document);

        var openCommand = new RelayCommand(() =>
            dockService.Show<SystemBuilderViewModel>(DockShowLocation.Document));
        
        windowService.RegisterUiExtension("MainWindow_RoundToolBarExtension",
            new OneWareUiExtension(_ =>
                new ExtensionButton(openCommand)
            ));
    }
}