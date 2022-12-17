using Playnite.SDK;
using PSNLibrary.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;

namespace PSNLibrary
{
    public class PSNLibrarySettings : ObservableObject
    {
        public bool ConnectAccount { get; set; } = true;
        public bool DownloadImageMetadata { get; set; } = true;
        public bool LastPlayed { get; set; } = false;
        public bool Playtime { get; set; } = false;
        public bool PS3 { get; set; } = true;
        public bool PSP { get; set; } = true;
        public bool PSVITA { get; set; } = true;
        public bool Migration { get; set; } = true;

        private string npsso = null;
        public string Npsso { get => npsso; set => SetValue(ref npsso, value); }
    }

    public class PSNLibrarySettingsViewModel : PluginSettingsViewModel<PSNLibrarySettings, PSNLibrary>
    {
        private PSNAccountClient clientApi;

        public bool IsUserLoggedIn
        {
            get
            {
                try
                {
                    clientApi.CheckAuthentication().GetAwaiter().GetResult();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public RelayCommand<object> LoginCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                Login();
            });
        }

        public RelayCommand<object> CheckAuthenticationCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                CheckAuthentication();
            });
        }

        public PSNLibrarySettingsViewModel(PSNLibrary plugin, IPlayniteAPI api) : base(plugin, api)
        {
            clientApi = new PSNAccountClient(plugin, api);
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new PSNLibrarySettings();
            }
        }

        private void Login()
        {
            Settings.Npsso = null;
            try
            {
                clientApi.Login();
                OnPropertyChanged(nameof(IsUserLoggedIn));
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                Logger.Error(e, "Failed to authenticate user.");
            }
        }
        private void CheckAuthentication()
        {
            clientApi.ClearAuthentication();
            OnPropertyChanged(nameof(IsUserLoggedIn));
        }
    }
}