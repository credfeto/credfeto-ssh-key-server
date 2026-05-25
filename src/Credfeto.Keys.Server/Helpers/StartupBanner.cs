using System;
using Figgle;

namespace Credfeto.Keys.Server.Helpers;

// https://www.figlet.org/examples.html
[GenerateFiggleText("Banner", "basic", "SSH Key Server")]
internal static partial class StartupBanner
{
    public static void Show()
    {
        Console.WriteLine(Banner);
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("Starting version " + VersionInformation.Version + "...");
    }
}
