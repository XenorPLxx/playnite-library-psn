using System.Windows.Controls;
using System.Windows.Navigation;

namespace PSNLibrary
{
  public partial class PSNLibrarySettingsView : UserControl
  {
    public PSNLibrarySettingsView()
    {
      InitializeComponent();
    }
    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
      System.Diagnostics.Process.Start(e.Uri.ToString());
    }
  }
}