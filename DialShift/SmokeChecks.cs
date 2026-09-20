using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DialShift.Core;

namespace DialShift;

internal static class SmokeChecks
{
    public static async Task Run(App app, string[] args)
    {
        var outputIndex = Array.IndexOf(args, "--output");
        var output = outputIndex >= 0 && args.Length > outputIndex + 1 ? args[outputIndex + 1] : Path.Combine(AppContext.BaseDirectory, "smoke");
        Directory.CreateDirectory(output);
        var checks = new List<object>();
        try
        {
            await Task.Delay(600);
            var window = (MainWindow)app.MainWindow;
            Capture(window, Path.Combine(output, "stations.png"));
            foreach (var station in app.Settings.Stations.ToArray())
            {
                app.Radio.Play(station);
                var deadline = DateTime.UtcNow.AddSeconds(18);
                while (!app.Radio.IsPlaying && DateTime.UtcNow < deadline) await Task.Delay(250);
                var playing = app.Radio.IsPlaying;
                await Task.Delay(2000);
                if (station == app.Settings.Stations[0]) Capture(window, Path.Combine(output, "playing.png"));
                checks.Add(new { name = "Live playback: " + station.Name, passed = playing && app.Radio.IsPlaying, status = app.Radio.Status });
            }
            app.Radio.Pause();
            await Task.Delay(1200);
            checks.Add(new { name = "Pause stops playback", passed = !app.Radio.IsActive && !app.Radio.IsPlaying });
            var slot = new ScheduleEntry { StationId = app.Settings.Stations[0].Id, Time = DateTime.Now.AddMinutes(-1).ToString("HH:mm"), Days = [DateTime.Now.AddMinutes(-1).DayOfWeek], Label = "Morning focus" };
            app.Settings.Schedule.Add(slot);
            app.Settings.ScheduleEnabled = true;
            app.Radio.RefreshSchedule();
            checks.Add(new { name = "Schedule catch-up selects current station", passed = app.Radio.Desired?.Id == slot.StationId && app.Radio.IsActive });
            app.Radio.Pause();
            await Task.Delay(1600);
            checks.Add(new { name = "Pause holds within current slot", passed = !app.Radio.IsActive });
            app.Radio.Play(app.Settings.Stations[1]);
            await Task.Delay(1500);
            checks.Add(new { name = "Manual selection holds within current slot", passed = app.Radio.Desired?.Id == app.Settings.Stations[1].Id });
            slot.Time = DateTime.Now.ToString("HH:mm"); slot.Days = [DateTime.Now.DayOfWeek];
            await Task.Delay(1600);
            checks.Add(new { name = "New schedule occurrence overrides manual station", passed = app.Radio.Desired?.Id == slot.StationId });
            app.Radio.Pause();
            app.Radio.ResumeFromSleep();
            checks.Add(new { name = "Resume preserves pause within same slot", passed = !app.Radio.IsActive });
            window.SelectPage(0); await Task.Delay(200); Capture(window, Path.Combine(output, "stations.png"));
            window.SelectPage(1); await Task.Delay(200); Capture(window, Path.Combine(output, "edit-stations.png"));
            window.SelectPage(2); await Task.Delay(200); Capture(window, Path.Combine(output, "schedule.png"));
            window.SelectPage(3); await Task.Delay(200); Capture(window, Path.Combine(output, "settings.png"));
            var stationEditor = new StationDialog(app, app.Settings.Stations[0]) { Owner = window }; stationEditor.Show(); await Task.Delay(150); Capture(stationEditor, Path.Combine(output, "station-editor.png")); stationEditor.Close();
            var scheduleEditor = new ScheduleDialog(app, slot, DateTime.Now.DayOfWeek) { Owner = window }; scheduleEditor.Show(); await Task.Delay(150); Capture(scheduleEditor, Path.Combine(output, "schedule-editor.png")); scheduleEditor.Close();
            window.Width = 780; window.Height = 650; window.SelectPage(0); await Task.Delay(200); Capture(window, Path.Combine(output, "compact.png"));
            app.HideToTray(); checks.Add(new { name = "Hide to tray", passed = !window.IsVisible });
            app.ShowWindow(); checks.Add(new { name = "Restore from tray", passed = window.IsVisible });
            app.Save(); var loaded = app.Store.Load(); checks.Add(new { name = "Persistence", passed = loaded.Schedule.Count == 1 && loaded.Volume == 0 });
            if (args.Contains("--recovery-test"))
            {
                app.Settings.ScheduleEnabled = false;
                app.Settings.FallbackStationId = app.Settings.Stations[0].Id;
                var unavailable = new Station { Name = "Unavailable test stream", Url = "http://127.0.0.1:1/unavailable" };
                app.Radio.Play(unavailable);
                var deadline = DateTime.UtcNow.AddSeconds(75);
                while (!(app.Radio.IsPlaying && app.Radio.Current?.Id == app.Settings.FallbackStationId) && DateTime.UtcNow < deadline) await Task.Delay(500);
                checks.Add(new { name = "Failed stream retries and plays fallback", passed = app.Radio.IsPlaying && app.Radio.Current?.Id == app.Settings.FallbackStationId, status = app.Radio.Status });
                app.Radio.Pause();
                await Task.Delay(1500);
                checks.Add(new { name = "Pause cancels fallback and retry", passed = !app.Radio.IsPlaying && !app.Radio.IsActive });
            }
        }
        catch (Exception ex) { checks.Add(new { name = "Unhandled smoke error", passed = false, error = ex.ToString() }); }
        finally
        {
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
            app.ExitApp();
        }
    }

    private static void Capture(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
