using System.Globalization;
using System.Text.Json;

namespace DialShift.Core;

public sealed class Station
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string Tag { get; set; } = "Internet radio";
    public override string ToString() => Name;
}

public sealed class ScheduleEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StationId { get; set; }
    public string Label { get; set; } = "";
    public string Time { get; set; } = "08:00";
    public List<DayOfWeek> Days { get; set; } = [];
    public bool Enabled { get; set; } = true;
}

public sealed class Settings
{
    public int Version { get; set; } = 1;
    public List<Station> Stations { get; set; } = [];
    public List<ScheduleEntry> Schedule { get; set; } = [];
    public int Volume { get; set; } = 60;
    public bool ScheduleEnabled { get; set; }
    public bool LaunchAtLogin { get; set; }
    public bool StartInTray { get; set; }
    public Guid? FallbackStationId { get; set; }
    public Guid? LastStationId { get; set; }
    public string VisualizerStyle { get; set; } = "Bars";

    public static Settings Defaults() => new()
    {
        Stations =
        [
            new() { Name = "Groove Salad", Tag = "SomaFM · Ambient / downtempo", Url = "https://ice5.somafm.com/groovesalad-128-aac" },
            new() { Name = "Drone Zone", Tag = "SomaFM · Atmospheric", Url = "https://ice5.somafm.com/dronezone-128-aac" },
            new() { Name = "Secret Agent", Tag = "SomaFM · Cinematic grooves", Url = "https://ice5.somafm.com/secretagent-128-aac" }
        ]
    };
}

public sealed record Occurrence(ScheduleEntry Entry, DateTime At)
{
    public string Key => $"{Entry.Id}:{At:yyyy-MM-ddTHH:mm}";
}

public static class Scheduler
{
    public static bool TryTime(string text, out TimeOnly time) => TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    // Local wall-clock time: a repeated DST hour has the same key and does not fire twice.
    // A skipped hour is caught up on the first tick after the clock jump, like wake from sleep.
    public static (Occurrence? Current, Occurrence? Next) Evaluate(Settings settings, DateTime now)
    {
        var ids = settings.Stations.Select(s => s.Id).ToHashSet();
        var occurrences = new List<Occurrence>();
        foreach (var entry in settings.Schedule.Where(e => e.Enabled && ids.Contains(e.StationId)))
        {
            if (!TryTime(entry.Time, out var time)) continue;
            for (var offset = -7; offset <= 7; offset++)
            {
                var date = now.Date.AddDays(offset);
                if (entry.Days.Contains(date.DayOfWeek)) occurrences.Add(new(entry, date.Add(time.ToTimeSpan())));
            }
        }
        return (occurrences.Where(o => o.At <= now).OrderByDescending(o => o.At).ThenBy(o => o.Entry.Id).FirstOrDefault(),
            occurrences.Where(o => o.At > now).OrderBy(o => o.At).ThenBy(o => o.Entry.Id).FirstOrDefault());
    }

    public static bool Conflicts(IEnumerable<ScheduleEntry> entries, ScheduleEntry candidate) => candidate.Enabled &&
        entries.Any(e => e.Id != candidate.Id && e.Enabled && e.Time == candidate.Time && e.Days.Intersect(candidate.Days).Any());
}

public sealed class ScheduleSession
{
    private Occurrence? last;

    public void HoldCurrent(Settings settings, DateTime now)
    {
        var current = Scheduler.Evaluate(settings, now).Current;
        if (last == null || current?.At >= last.At) last = current;
    }

    public Occurrence? TakeChange(Settings settings, DateTime now, bool force = false)
    {
        if (!settings.ScheduleEnabled) return null;
        var current = Scheduler.Evaluate(settings, now).Current;
        if (current == null) { last = null; return null; }
        // Do not replay an older occurrence when the wall clock moves backwards.
        if (!force && last != null && (current.Key == last.Key || current.At < last.At)) return null;
        last = current;
        return current;
    }
}

public sealed class SettingsStore(string directory)
{
    public string DirectoryPath { get; } = directory;
    public string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public string? Warning { get; private set; }
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public Settings Load()
    {
        if (!File.Exists(FilePath)) return Settings.Defaults();
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), JsonOptions) ?? throw new JsonException("Empty settings.");
            if (settings.Version != 1) throw new JsonException("Unsupported settings version.");
            if (settings.Stations is null || settings.Schedule is null || settings.Stations.Any(s => s is null || string.IsNullOrWhiteSpace(s.Name) || !ValidUrl(s.Url)) || settings.Schedule.Any(e => e is null || e.Days is null))
                throw new JsonException("Invalid settings data.");
            settings.Volume = Math.Clamp(settings.Volume, 0, 100);
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            var backup = FilePath + ".unreadable-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            // Preserve the original before allowing defaults to be saved.
            File.Copy(FilePath, backup);
            Warning = $"Your settings could not be read. A copy was preserved at {backup}.";
            return Settings.Defaults();
        }
    }

    public void Save(Settings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(file, settings, JsonOptions);
            file.Flush(true);
        }
        File.Move(temporary, FilePath, true);
    }

    public static bool ValidUrl(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http") && !string.IsNullOrWhiteSpace(uri.Host);
}
