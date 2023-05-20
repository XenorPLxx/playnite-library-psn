using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PSNLibrary
{
    public class PSNLibrary : LibraryPlugin
    {
        // Playnite API access
        private readonly INotificationsAPI notifications;
        private static readonly ILogger logger = LogManager.GetLogger();

        // Settings access https://api.playnite.link/docs/master/tutorials/extensions/pluginSettings.html
        private PSNLibrarySettingsViewModel settings { get; set; }

        // LibraryPlugin properties https://api.playnite.link/docs/master/api/Playnite.SDK.Plugins.LibraryPlugin.html#properties
        public override Guid Id { get; } = Guid.Parse("e4ac81cb-1b1a-4ec9-8639-9a9633989a71");
        public override LibraryClient Client { get; } = new PSNLibraryClient();
        public override string LibraryBackground => null;
        public override string LibraryIcon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");
        public override string Name => "PlayStation";

        // LibraryPlugin constructor 
        public PSNLibrary(IPlayniteAPI api) : base(api)
        {
            notifications = api.Notifications;
            settings = new PSNLibrarySettingsViewModel(this);
            Properties = new LibraryPluginProperties
            {
                CanShutdownClient = false,
                HasCustomizedGameImport = true,
                HasSettings = true
            };
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            // Return list of user's games.
            return new List<GameMetadata>()
            {
                new GameMetadata()
                {
                    Name = "Notepad",
                    GameId = "notepad",
                    GameActions = new List<GameAction>
                    {
                        new GameAction()
                        {
                            Type = GameActionType.File,
                            Path = "notepad.exe",
                            IsPlayAction = true
                        }
                    },
                    IsInstalled = true,
                    Icon = new MetadataFile(@"c:\Windows\notepad.exe")
                },
                new GameMetadata()
                {
                    Name = "Calculator",
                    GameId = "calc",
                    GameActions = new List<GameAction>
                    {
                        new GameAction()
                        {
                            Type = GameActionType.File,
                            Path = "calc.exe",
                            IsPlayAction = true
                        }
                    },
                    IsInstalled = true,
                    Icon = new MetadataFile(@"https://playnite.link/applogo.png"),
                    BackgroundImage = new MetadataFile(@"https://playnite.link/applogo.png")
                }
            };
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new PSNLibrarySettingsView();
        }
    }
}