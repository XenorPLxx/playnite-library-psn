using Playnite.SDK;
using Playnite.SDK.Data;
using PSNLibrary.Services;
using System;
using System.Collections.Generic;

namespace PSNLibrary
{
  public class PSNLibrarySettings : ObservableObject
  {
    public bool connectAccount = true;
    // Existing "LastPlayed" settings deserialize into this value. New installs follow Playnite's normal import behavior by default.
    public bool lastPlayed = false;
    // Existing "Playtime" settings deserialize into this value. New installs follow Playnite's global policy by default.
    public bool playtime = false;
    // Existing "PlayCount" settings deserialize into this value. New installs only sync play count when explicitly requested.
    public bool playCount = false;
    public bool ps3 = true;
    public bool psp = true;
    public bool psvita = true;
    public bool pc = true;
    public bool tags = true;
    public bool noTags = false;
    public bool plusSource = false;
    private string npsso = string.Empty;

    public bool ConnectAccount { get => connectAccount; set => SetValue(ref connectAccount, value); }
    [SerializationPropertyName("LastPlayed")]
    public bool AlwaysUpdateExistingLastPlayed { get => lastPlayed; set => SetValue(ref lastPlayed, value); }
    [SerializationPropertyName("Playtime")]
    public bool AlwaysUpdateExistingPlaytime { get => playtime; set => SetValue(ref playtime, value); }
    [SerializationPropertyName("PlayCount")]
    public bool AlwaysUpdateExistingPlayCount { get => playCount; set => SetValue(ref playCount, value); }
    public bool PS3 { get => ps3; set => SetValue(ref ps3, value); }
    public bool PSP { get => psp; set => SetValue(ref psp, value); }
    public bool PSVITA { get => psvita; set => SetValue(ref psvita, value); }
    public bool PC { get => pc; set => SetValue(ref pc, value); }
    public bool Tags { get => tags; set => SetValue(ref tags, value); }
    public bool NoTags { get => noTags; set => SetValue(ref noTags, value); }
    public bool PlusSource { get => plusSource; set => SetValue(ref plusSource, value); }
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

    public RelayCommand<object> CheckAuthenticationCommand
    {
      get => new RelayCommand<object>((a) =>
      {
        CheckAuthentication();
      });
    }

    private void CheckAuthentication()
    {
      clientApi.ClearAuthentication();
      OnPropertyChanged(nameof(IsUserLoggedIn));
    }
  }
}
