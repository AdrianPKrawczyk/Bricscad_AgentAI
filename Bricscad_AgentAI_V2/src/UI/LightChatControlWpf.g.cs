// Plik generowany automatycznie przez csc.exe (obchodzi MSBuild XAML pipeline).
// Zawiera minimalny partial stub dla LightChatControlWpf.xaml.cs aby build dzialal
// bez uruchamiania pelnego WPF MSBuild target.
//
// W runtime (BricsCAD) ten plik jest NADPISYWANY przez prawdziwy .g.cs
// generowany z XAML - my zapewniamy jedynie INICJALIZACJE dla samej kompilacji.
using System;
using System.Windows;
using System.Windows.Controls;

namespace Bricscad_AgentAI_V2.UI
{
    public partial class LightChatControlWpf : UserControl
    {
        // Pusty partial - kompilator C# pozwala na wywolanie w konstruktorze
        // "InitializeComponent()" zdefiniowanego w innym pliku partial.
        // W runtime potrzebny jest prawdziwy plik .g.cs (generowany przez MSBuild
        // WPF targets z pliku .xaml). W dev-build (BricsCAD) to dziala.
        // W naszym bezposrednim csc build - symbol istnieje, wiec AgentStartup
        // moze z niego korzystac.
        private void InitializeComponent()
        {
            // Pusty stub - wystarcza do kompilacji
        }
    }
}
