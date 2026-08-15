using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace PlayStationLibrary;

public partial class PlayStationLibrarySettingsView : UserControl
{
    // PasswordBox.Password cannot be data-bound, so it is kept in sync by hand.
    private bool suppressSync;

    public PlayStationLibrarySettingsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SyncPasswordBoxFromSettings();
        Loaded += (_, _) => SyncPasswordBoxFromSettings();
    }

    private PlayStationLibrarySettingsHandler? Handler => DataContext as PlayStationLibrarySettingsHandler;

    private void NpssoPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (suppressSync || Handler == null)
        {
            return;
        }

        suppressSync = true;
        Handler.Settings.Npsso = NpssoPasswordBox.Password;
        suppressSync = false;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        // WPF does not open links itself; the shell has to be asked.
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void RevealNpsso_Toggled(object sender, RoutedEventArgs e)
    {
        SyncPasswordBoxFromSettings();
    }

    private void SyncPasswordBoxFromSettings()
    {
        if (suppressSync || Handler == null)
        {
            return;
        }

        suppressSync = true;
        NpssoPasswordBox.Password = Handler.Settings.Npsso ?? string.Empty;
        suppressSync = false;
    }
}
