using System.IO;
using System.Text.Json;
using Playnite;

namespace PlayStationLibrary;

/// <summary>
/// Remembers the play time each game had at the previous import so that the increase since then can
/// be turned into a play session.
/// </summary>
/// <remarks>
/// PlayStation only exposes a running total, never the individual sessions behind it, so these are
/// an approximation: everything played between two imports collapses into a single session dated to
/// the last time the game was played. Nothing before the first import can be recovered at all.
/// PlayStation games cannot be launched through Playnite, so nothing else ever records sessions for
/// them and there is no risk of counting the same play twice.
/// </remarks>
public sealed class PlayStationPlayTimeHistory
{
    private static readonly ILogger logger = LogManager.GetLogger();
    private readonly string historyPath;
    private readonly Dictionary<string, uint> playTimeByGameId;

    public PlayStationPlayTimeHistory(string userDataDir)
    {
        historyPath = Path.Combine(userDataDir, "playtime.json");
        playTimeByGameId = Read();
    }

    /// <summary>
    /// Returns the session that accounts for the play time gained since the last import, or null
    /// when the game has not been played since.
    /// </summary>
    public ImportableGameSession? GetSessionSincePreviousImport(string gameId, uint playTime, DateTimeOffset? lastPlayed)
    {
        if (playTime == 0 || lastPlayed == null)
        {
            return null;
        }

        // Without a previous reading there is nothing to compare against: everything played before
        // the first import is unknown, so reporting it as one session would invent history.
        if (!playTimeByGameId.TryGetValue(gameId, out var previousPlayTime) || playTime <= previousPlayTime)
        {
            return null;
        }

        var length = playTime - previousPlayTime;

        // The date and the new total are both part of the id. PlayStation can report more play time
        // without moving the last-played date, and those are genuinely different sessions that must
        // not collide with one already recorded.
        var sessionId = $"psn_{gameId}_{lastPlayed.Value.ToUnixTimeSeconds()}_{playTime}";
        return new ImportableGameSession(sessionId, lastPlayed.Value, length);
    }

    public void Record(string gameId, uint playTime)
    {
        playTimeByGameId[gameId] = playTime;
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(historyPath, JsonSerializer.Serialize(playTimeByGameId));
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to store PlayStation play time history.");
        }
    }

    private Dictionary<string, uint> Read()
    {
        if (!File.Exists(historyPath))
        {
            return new Dictionary<string, uint>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, uint>>(File.ReadAllText(historyPath))
                ?? new Dictionary<string, uint>(StringComparer.Ordinal);
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to read PlayStation play time history.");
            return new Dictionary<string, uint>(StringComparer.Ordinal);
        }
    }
}
