using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Playnite;

namespace PlayStationLibrary;

public partial class PlayStationLibraryPluginSettings : ObservableObject
{
    [ObservableProperty] private bool connectAccount = true;

    /// <summary>The NPSSO in the clear. Never serialized; <see cref="ProtectedNpsso"/> is.</summary>
    private string npsso = string.Empty;

    [JsonIgnore]
    public string Npsso
    {
        get => npsso;
        set => SetProperty(ref npsso, value);
    }

    /// <summary>DPAPI-protected form of <see cref="Npsso"/>, as written to settings.json.</summary>
    [JsonPropertyName("Npsso")]
    public string? ProtectedNpsso
    {
        get => PlayStationSecrets.Protect(Npsso);
        set => Npsso = PlayStationSecrets.UnprotectOrMigrate(value);
    }

    // Trophy-derived platforms. These are inclusion filters, not cosmetic labels: the trophy API is
    // the only source covering these systems, and a title it reports is imported only when at least
    // one of its platforms is enabled here.
    [ObservableProperty] private bool importPs3 = true;
    [ObservableProperty] private bool importPsp = true;
    [ObservableProperty] private bool importPsVita = true;

    /// <summary>Empty follows the Playnite UI language. Controls localized trophy text.</summary>
    [ObservableProperty] private string locale = PlayStationLocales.Automatic;

    /// <summary>
    /// Imports games that appear only in the trophy list, such as disc games that PlayStation never
    /// reports as a purchase. Off by default because these can only be matched to existing games by
    /// name, so a mismatch shows up as a duplicate.
    /// </summary>
    [ObservableProperty] private bool importTrophyOnlyGames;

    [ObservableProperty] private bool importPlusTag = true;
    [ObservableProperty] private bool usePlusSource;
}

[INotifyPropertyChanged]
public partial class PlayStationLibrarySettingsHandler : PluginSettingsHandler
{
    private const string SettingsErrorNotification = "playstation_settings_error";

    private static readonly ILogger logger = LogManager.GetLogger();
    private readonly PlayStationLibraryPlugin plugin;
    private CancellationTokenSource? authenticationCheckCancellation;

    [ObservableProperty] private PlayStationLibraryPluginSettings settings = new();
    [ObservableProperty] private bool? isUserLoggedIn;
    [ObservableProperty] private bool isAuthenticationCheckInProgress;
    [ObservableProperty] private string? authenticationCheckError;

    public IReadOnlyList<PlayStationLocale> Locales { get; } = PlayStationLocales.All;

    public string AuthenticationStatus => IsUserLoggedIn switch
    {
        _ when IsAuthenticationCheckInProgress => Loc.login_checking(),
        _ when !string.IsNullOrEmpty(AuthenticationCheckError) => AuthenticationCheckError,
        true => Loc.logged_in(),
        false => Loc.not_logged_in(),
        _ => Loc.playstation_auth_not_checked()
    };

    public PlayStationLibrarySettingsHandler(PlayStationLibraryPlugin plugin, Plugin.GetSettingsHandlerArgs settingsArgs)
    {
        this.plugin = plugin;
    }

    public override UserControl GetEditView(GetSettingsViewArgs args)
    {
        return new PlayStationLibrarySettingsView { DataContext = this };
    }

    public override Task BeginEditAsync(BeginEditArgs args)
    {
        CancelAuthenticationCheck();
        Settings = Clone(plugin.Settings);
        IsUserLoggedIn = null;
        AuthenticationCheckError = null;
        IsAuthenticationCheckInProgress = false;
        // This mirrors P10: opening the settings validates the persisted authentication without
        // blocking the settings window. UpdateIsUserLoggedInAsync handles every result itself.
        _ = UpdateIsUserLoggedInAsync();
        return Task.CompletedTask;
    }

    public override Task CancelEditAsync(CancelEditArgs args)
    {
        CancelAuthenticationCheck();
        Settings = Clone(plugin.Settings);
        return Task.CompletedTask;
    }

    public override Task EndEditAsync(EndEditArgs args)
    {
        CancelAuthenticationCheck();
        // A different NPSSO may belong to a different account, so the token derived from the
        // previous one must not survive the change.
        if (!string.Equals(Settings.Npsso, plugin.Settings.Npsso, StringComparison.Ordinal))
        {
            new PlayStationAccountClient(plugin.PlayniteApi).ClearAuthentication();
        }

        if (!plugin.SaveSettings(Clone(Settings)))
        {
            // The dialog is already closing, so a notification outlives it where a dialog would not.
            plugin.PlayniteApi.Notifications.Add(new NotificationMessage(
                SettingsErrorNotification,
                Loc.playstation_settings_save_failed(),
                NotificationSeverity.Error,
                async () => await plugin.PlayniteApi.MainView.OpenPluginSettingsAsync(PlayStationLibraryPlugin.Id)));
        }
        else
        {
            plugin.PlayniteApi.Notifications.Remove(SettingsErrorNotification);
        }

        return Task.CompletedTask;
    }

    public override Task<ICollection<string>> VerifySettingsAsync(VerifySettingsArgs args)
    {
        if (PlayStationAccountClient.TryGetNpsso(Settings.Npsso, out var npsso, out var error))
        {
            Settings.Npsso = npsso;
            return Task.FromResult<ICollection<string>>([]);
        }

        return Task.FromResult<ICollection<string>>([error]);
    }

    [RelayCommand]
    private async Task CheckAuthenticationAsync()
    {
        if (!PlayStationAccountClient.TryGetNpsso(Settings.Npsso, out var npsso, out var error))
        {
            await plugin.PlayniteApi.Dialogs.ShowMessageAsync(error, Loc.playstation_invalid_npsso_title());
            return;
        }

        Settings.Npsso = npsso;

        // The check has to exercise the pasted value rather than a token left over from an earlier
        // one, but a failing check must not cost the user a session that was still valid, so the
        // stored token is put back unless the NPSSO proved itself.
        var client = new PlayStationAccountClient(plugin.PlayniteApi);
        var storedAuthentication = client.TakeAuthentication();
        await UpdateIsUserLoggedInAsync();
        if (IsUserLoggedIn != true)
        {
            client.RestoreAuthentication(storedAuthentication);
        }
    }

    private async Task UpdateIsUserLoggedInAsync()
    {
        CancelAuthenticationCheck();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        authenticationCheckCancellation = cancellation;
        IsUserLoggedIn = null;
        AuthenticationCheckError = null;
        IsAuthenticationCheckInProgress = true;
        try
        {
            var client = new PlayStationAccountClient(plugin.PlayniteApi);
            var isUserLoggedIn = await client.GetIsUserLoggedInAsync(Settings.Npsso, cancellation.Token);
            if (ReferenceEquals(authenticationCheckCancellation, cancellation))
            {
                IsUserLoggedIn = isUserLoggedIn;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (ReferenceEquals(authenticationCheckCancellation, cancellation))
            {
                AuthenticationCheckError = Loc.playstation_auth_timed_out();
            }
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to check PlayStation authentication.");
            if (ReferenceEquals(authenticationCheckCancellation, cancellation))
            {
                IsUserLoggedIn = false;
                AuthenticationCheckError = Loc.playstation_auth_failed();
            }
        }
        finally
        {
            if (ReferenceEquals(authenticationCheckCancellation, cancellation))
            {
                IsAuthenticationCheckInProgress = false;
                authenticationCheckCancellation = null;
            }
        }
    }

    partial void OnIsUserLoggedInChanged(bool? value)
    {
        OnPropertyChanged(nameof(AuthenticationStatus));
    }

    partial void OnIsAuthenticationCheckInProgressChanged(bool value)
    {
        OnPropertyChanged(nameof(AuthenticationStatus));
    }

    partial void OnAuthenticationCheckErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(AuthenticationStatus));
    }

    private void CancelAuthenticationCheck()
    {
        var cancellation = authenticationCheckCancellation;
        authenticationCheckCancellation = null;
        cancellation?.Cancel();
    }

    internal static PlayStationLibraryPluginSettings LoadSettings(string userDataDir)
    {
        var path = Path.Combine(userDataDir, "settings.json");
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<PlayStationLibraryPluginSettings>(File.ReadAllText(path)) ?? new PlayStationLibraryPluginSettings()
                : new PlayStationLibraryPluginSettings();
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to load PlayStation settings.");
            return new PlayStationLibraryPluginSettings();
        }
    }

    /// <summary>
    /// Writes the settings, reporting rather than throwing when the user data folder cannot be
    /// written. Throwing here would surface as the whole settings dialog failing to close.
    /// </summary>
    internal static bool SaveSettings(string userDataDir, PlayStationLibraryPluginSettings settings)
    {
        try
        {
            Directory.CreateDirectory(userDataDir);
            var path = Path.Combine(userDataDir, "settings.json");
            File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to save PlayStation settings.");
            return false;
        }
    }

    private static PlayStationLibraryPluginSettings Clone(PlayStationLibraryPluginSettings source)
    {
        return new PlayStationLibraryPluginSettings
        {
            ConnectAccount = source.ConnectAccount,
            Npsso = source.Npsso,
            ImportPs3 = source.ImportPs3,
            ImportPsp = source.ImportPsp,
            ImportPsVita = source.ImportPsVita,
            Locale = source.Locale,
            ImportTrophyOnlyGames = source.ImportTrophyOnlyGames,
            ImportPlusTag = source.ImportPlusTag,
            UsePlusSource = source.UsePlusSource
        };
    }
}
