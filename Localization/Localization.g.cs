namespace Playnite;

public static partial class Loc
{

    /// <summary>
    /// Failed to import games from {$libName}
    /// </summary>
    public static string library_import_error(object libName)
    {
        return GetString("library_import_error", ("libName", libName));
    }
    /// <summary>
    /// Connect account
    /// </summary>
    public static string settings_connect_account()
    {
        return GetString("settings_connect_account");
    }
    /// <summary>
    /// Check authentication
    /// </summary>
    public static string authenticate_label()
    {
        return GetString("authenticate_label");
    }
    /// <summary>
    /// Checking authentication status…
    /// </summary>
    public static string login_checking()
    {
        return GetString("login_checking");
    }
    /// <summary>
    /// User is authenticated
    /// </summary>
    public static string logged_in()
    {
        return GetString("logged_in");
    }
    /// <summary>
    /// Requires authentication
    /// </summary>
    public static string not_logged_in()
    {
        return GetString("not_logged_in");
    }
    /// <summary>
    /// PlayStation Library
    /// </summary>
    public static string playstation_library_label()
    {
        return GetString("playstation_library_label");
    }
    /// <summary>
    /// Authentication
    /// </summary>
    public static string playstation_authentication_label()
    {
        return GetString("playstation_authentication_label");
    }
    /// <summary>
    /// NPSSO
    /// </summary>
    public static string playstation_npsso_label()
    {
        return GetString("playstation_npsso_label");
    }
    /// <summary>
    /// Show or hide the NPSSO
    /// </summary>
    public static string playstation_npsso_toggle_tooltip()
    {
        return GetString("playstation_npsso_toggle_tooltip");
    }
    /// <summary>
    /// Trophy language
    /// </summary>
    public static string playstation_region_label()
    {
        return GetString("playstation_region_label");
    }
    /// <summary>
    /// Requests localized trophy names and descriptions. The next trophies update after changing it refreshes PlayStation trophies and may take a while. PlayStation may fall back when a trophy has no translation in the selected language.
    /// </summary>
    public static string playstation_region_description()
    {
        return GetString("playstation_region_description");
    }
    /// <summary>
    /// Legacy platforms
    /// </summary>
    public static string playstation_legacy_platforms_label()
    {
        return GetString("playstation_legacy_platforms_label");
    }
    /// <summary>
    /// These systems are only listed by the trophy API, so games for them are imported from your trophy list. Disabling a platform excludes those games from the import.
    /// </summary>
    public static string playstation_legacy_platforms_description()
    {
        return GetString("playstation_legacy_platforms_description");
    }
    /// <summary>
    /// PlayStation 3
    /// </summary>
    public static string playstation_platform_ps3()
    {
        return GetString("playstation_platform_ps3");
    }
    /// <summary>
    /// PlayStation Portable
    /// </summary>
    public static string playstation_platform_psp()
    {
        return GetString("playstation_platform_psp");
    }
    /// <summary>
    /// PlayStation Vita
    /// </summary>
    public static string playstation_platform_psvita()
    {
        return GetString("playstation_platform_psvita");
    }
    /// <summary>
    /// Trophy-only games
    /// </summary>
    public static string playstation_trophy_only_label()
    {
        return GetString("playstation_trophy_only_label");
    }
    /// <summary>
    /// Import games that appear only in your trophy list
    /// </summary>
    public static string playstation_trophy_only_checkbox()
    {
        return GetString("playstation_trophy_only_checkbox");
    }
    /// <summary>
    /// Finds disc games that PlayStation never reports as a purchase. These can only be matched to games you already have by name, so a title whose names differ may be imported a second time. Add-ons and expansions may also appear.
    /// </summary>
    public static string playstation_trophy_only_description()
    {
        return GetString("playstation_trophy_only_description");
    }
    /// <summary>
    /// PlayStation Plus
    /// </summary>
    public static string playstation_plus_label()
    {
        return GetString("playstation_plus_label");
    }
    /// <summary>
    /// Tag games included with PlayStation Plus
    /// </summary>
    public static string playstation_plus_tag_checkbox()
    {
        return GetString("playstation_plus_tag_checkbox");
    }
    /// <summary>
    /// Use a separate ‘PlayStation Plus’ source for games you do not own outright
    /// </summary>
    public static string playstation_plus_source_checkbox()
    {
        return GetString("playstation_plus_source_checkbox");
    }
    /// <summary>
    /// PlayStation settings could not be saved. Check that the Playnite user data folder is writable.
    /// </summary>
    public static string playstation_settings_save_failed()
    {
        return GetString("playstation_settings_save_failed");
    }
    /// <summary>
    /// PlayStation achievements could not be updated.
    /// </summary>
    public static string playstation_achievements_error()
    {
        return GetString("playstation_achievements_error");
    }
    /// <summary>
    /// 1. Sign in to PlayStation in your normal browser and choose ‘Trust this Browser’.
    /// </summary>
    public static string playstation_auth_step_browser()
    {
        return GetString("playstation_auth_step_browser");
    }
    /// <summary>
    /// 2. Open
    /// </summary>
    public static string playstation_auth_step_open()
    {
        return GetString("playstation_auth_step_open");
    }
    /// <summary>
    /// in that browser.
    /// </summary>
    public static string playstation_auth_step_open_suffix()
    {
        return GetString("playstation_auth_step_open_suffix");
    }
    /// <summary>
    /// 3. Paste either its NPSSO value or the complete {"npsso":"…"} response below.
    /// </summary>
    public static string playstation_auth_step_paste()
    {
        return GetString("playstation_auth_step_paste");
    }
    /// <summary>
    /// Not checked
    /// </summary>
    public static string playstation_auth_not_checked()
    {
        return GetString("playstation_auth_not_checked");
    }
    /// <summary>
    /// Authentication check timed out.
    /// </summary>
    public static string playstation_auth_timed_out()
    {
        return GetString("playstation_auth_timed_out");
    }
    /// <summary>
    /// Authentication check failed. See the Playnite log for details.
    /// </summary>
    public static string playstation_auth_failed()
    {
        return GetString("playstation_auth_failed");
    }
    /// <summary>
    /// Invalid NPSSO
    /// </summary>
    public static string playstation_invalid_npsso_title()
    {
        return GetString("playstation_invalid_npsso_title");
    }
}

public static partial class LocId
{

    /// <summary>
    /// Failed to import games from {$libName}
    /// </summary>
    public const string library_import_error = "library_import_error";
    /// <summary>
    /// Connect account
    /// </summary>
    public const string settings_connect_account = "settings_connect_account";
    /// <summary>
    /// Check authentication
    /// </summary>
    public const string authenticate_label = "authenticate_label";
    /// <summary>
    /// Checking authentication status…
    /// </summary>
    public const string login_checking = "login_checking";
    /// <summary>
    /// User is authenticated
    /// </summary>
    public const string logged_in = "logged_in";
    /// <summary>
    /// Requires authentication
    /// </summary>
    public const string not_logged_in = "not_logged_in";
    /// <summary>
    /// PlayStation Library
    /// </summary>
    public const string playstation_library_label = "playstation_library_label";
    /// <summary>
    /// Authentication
    /// </summary>
    public const string playstation_authentication_label = "playstation_authentication_label";
    /// <summary>
    /// NPSSO
    /// </summary>
    public const string playstation_npsso_label = "playstation_npsso_label";
    /// <summary>
    /// Show or hide the NPSSO
    /// </summary>
    public const string playstation_npsso_toggle_tooltip = "playstation_npsso_toggle_tooltip";
    /// <summary>
    /// Trophy language
    /// </summary>
    public const string playstation_region_label = "playstation_region_label";
    /// <summary>
    /// Requests localized trophy names and descriptions. The next trophies update after changing it refreshes PlayStation trophies and may take a while. PlayStation may fall back when a trophy has no translation in the selected language.
    /// </summary>
    public const string playstation_region_description = "playstation_region_description";
    /// <summary>
    /// Legacy platforms
    /// </summary>
    public const string playstation_legacy_platforms_label = "playstation_legacy_platforms_label";
    /// <summary>
    /// These systems are only listed by the trophy API, so games for them are imported from your trophy list. Disabling a platform excludes those games from the import.
    /// </summary>
    public const string playstation_legacy_platforms_description = "playstation_legacy_platforms_description";
    /// <summary>
    /// PlayStation 3
    /// </summary>
    public const string playstation_platform_ps3 = "playstation_platform_ps3";
    /// <summary>
    /// PlayStation Portable
    /// </summary>
    public const string playstation_platform_psp = "playstation_platform_psp";
    /// <summary>
    /// PlayStation Vita
    /// </summary>
    public const string playstation_platform_psvita = "playstation_platform_psvita";
    /// <summary>
    /// Trophy-only games
    /// </summary>
    public const string playstation_trophy_only_label = "playstation_trophy_only_label";
    /// <summary>
    /// Import games that appear only in your trophy list
    /// </summary>
    public const string playstation_trophy_only_checkbox = "playstation_trophy_only_checkbox";
    /// <summary>
    /// Finds disc games that PlayStation never reports as a purchase. These can only be matched to games you already have by name, so a title whose names differ may be imported a second time. Add-ons and expansions may also appear.
    /// </summary>
    public const string playstation_trophy_only_description = "playstation_trophy_only_description";
    /// <summary>
    /// PlayStation Plus
    /// </summary>
    public const string playstation_plus_label = "playstation_plus_label";
    /// <summary>
    /// Tag games included with PlayStation Plus
    /// </summary>
    public const string playstation_plus_tag_checkbox = "playstation_plus_tag_checkbox";
    /// <summary>
    /// Use a separate ‘PlayStation Plus’ source for games you do not own outright
    /// </summary>
    public const string playstation_plus_source_checkbox = "playstation_plus_source_checkbox";
    /// <summary>
    /// PlayStation settings could not be saved. Check that the Playnite user data folder is writable.
    /// </summary>
    public const string playstation_settings_save_failed = "playstation_settings_save_failed";
    /// <summary>
    /// PlayStation achievements could not be updated.
    /// </summary>
    public const string playstation_achievements_error = "playstation_achievements_error";
    /// <summary>
    /// 1. Sign in to PlayStation in your normal browser and choose ‘Trust this Browser’.
    /// </summary>
    public const string playstation_auth_step_browser = "playstation_auth_step_browser";
    /// <summary>
    /// 2. Open
    /// </summary>
    public const string playstation_auth_step_open = "playstation_auth_step_open";
    /// <summary>
    /// in that browser.
    /// </summary>
    public const string playstation_auth_step_open_suffix = "playstation_auth_step_open_suffix";
    /// <summary>
    /// 3. Paste either its NPSSO value or the complete {"npsso":"…"} response below.
    /// </summary>
    public const string playstation_auth_step_paste = "playstation_auth_step_paste";
    /// <summary>
    /// Not checked
    /// </summary>
    public const string playstation_auth_not_checked = "playstation_auth_not_checked";
    /// <summary>
    /// Authentication check timed out.
    /// </summary>
    public const string playstation_auth_timed_out = "playstation_auth_timed_out";
    /// <summary>
    /// Authentication check failed. See the Playnite log for details.
    /// </summary>
    public const string playstation_auth_failed = "playstation_auth_failed";
    /// <summary>
    /// Invalid NPSSO
    /// </summary>
    public const string playstation_invalid_npsso_title = "playstation_invalid_npsso_title";
}
