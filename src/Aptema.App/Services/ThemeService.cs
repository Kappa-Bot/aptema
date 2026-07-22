using Microsoft.Win32;
using System.Windows;

namespace Aptema.App.Services;

public static class ThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsReducedMotion => !SystemParameters.ClientAreaAnimation || !SystemParameters.MenuAnimation;

    public static void ApplySystemTheme(ResourceDictionary resources)
    {
        var source = SystemParameters.HighContrast
            ? "Themes/Colors.HighContrast.xaml"
            : IsLightTheme() ? "Themes/Colors.Light.xaml" : "Themes/Colors.Dark.xaml";
        var dictionaries = resources.MergedDictionaries;
        if (dictionaries.Count == 0)
        {
            dictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
            return;
        }

        dictionaries[0] = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };
    }

    private static bool IsLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }
}
