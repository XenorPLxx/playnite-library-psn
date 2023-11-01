using Playnite.SDK;
using Playnite.SDK.Data;
using PSNLibrary.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PSNLibrary
{
  public class PSNLibrarySettings : ObservableObject
  {
    public bool connectAccount = true;
    public bool downloadImageMetadata = true;
    public bool lastPlayed = true;
    public bool playtime = true;
    public bool playCount = true;
    public bool ps3 = true;
    public bool psp = true;
    public bool psvita = true;
    public bool migration = true;
    public bool tags = true;
    public bool noTags = false;
    private string npsso = string.Empty;

    public bool ConnectAccount { get => connectAccount; set => SetValue(ref connectAccount, value); }
    public bool DownloadImageMetadata { get => downloadImageMetadata; set => SetValue(ref downloadImageMetadata, value); }
    public bool LastPlayed { get => lastPlayed; set => SetValue(ref lastPlayed, value); }
    public bool Playtime { get => playtime; set => SetValue(ref playtime, value); }
    public bool PlayCount { get => playCount; set => SetValue(ref playCount, value); }
    public bool PS3 { get => ps3; set => SetValue(ref ps3, value); }
    public bool PSP { get => psp; set => SetValue(ref psp, value); }
    public bool PSVITA { get => psvita; set => SetValue(ref psvita, value); }
    public bool Migration { get => migration; set => SetValue(ref migration, value); }
    public bool Tags { get => tags; set => SetValue(ref tags, value); }
    public bool NoTags { get => noTags; set => SetValue(ref noTags, value); }
    public string Npsso { get => npsso; set => SetValue(ref npsso, value); }
  }

  public class PSNLibrarySettingsViewModel : ObservableObject, ISettings
  {
    private readonly PSNLibrary plugin;
    private PSNLibrarySettings editingClone { get; set; }

    private PSNLibrarySettings settings;
    public PSNLibrarySettings Settings
    {
      get => settings;
      set
      {
        settings = value;
        OnPropertyChanged();
      }
    }

    public PSNLibrarySettingsViewModel(PSNLibrary plugin)
    {
      clientApi = new PSNClient(plugin);
      // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
      this.plugin = plugin;

      // Load saved settings.
      var savedSettings = plugin.LoadPluginSettings<PSNLibrarySettings>();

      // LoadPluginSettings returns null if no saved data is available.
      if (savedSettings != null)
      {
        Settings = savedSettings;
      }
      else
      {
        Settings = new PSNLibrarySettings();
      }


    }

    public void BeginEdit()
    {
      // Code executed when settings view is opened and user starts editing values.
      editingClone = Serialization.GetClone(Settings);
    }

    public void CancelEdit()
    {
      // Code executed when user decides to cancel any changes made since BeginEdit was called.
      // This method should revert any changes made to Option1 and Option2.
      Settings = editingClone;
    }

    public void EndEdit()
    {
      // Code executed when user decides to confirm changes made since BeginEdit was called.
      // This method should save settings made to Option1 and Option2.
      plugin.SavePluginSettings(Settings);
    }

    public bool VerifySettings(out List<string> errors)
    {
      // Code execute when user decides to confirm changes made since BeginEdit was called.
      // Executed before EndEdit is called and EndEdit is not called if false is returned.
      // List of errors is presented to user if verification fails.
      errors = new List<string>();
      return true;
    }

    // Refactorable
    private PSNClient clientApi;
    public bool IsUserLoggedIn
    {
      get
      {
        return Services.CheckAuthentication.call(clientApi);
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
        // Logger.Error(e, "Failed to authenticate user.");
      }
    }
    private void CheckAuthentication()
    {
      clientApi.ClearAuthentication();
      OnPropertyChanged(nameof(IsUserLoggedIn));
    }
  }
}