using CatFeeder.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;

namespace CatFeeder.Application;

public class FeederService
{
    private readonly User _user;
    public FeederService(User user) => _user = user;

    public double TotalDispensed { get; private set; }
    public IReadOnlyList<Feeder> Feeders => _user.Feeders;
    public IReadOnlyList<Schedule> Schedules => _user.Schedules;

    // ---------- Feeders CRUD ----------
    public Feeder AddFeeder(FeederType type, string name, double capacity)
    {
        Feeder f = type == FeederType.Dispenser ? new DispenserFeeder() : new WeightSensorFeeder();
        f.Name = name; f.UserId = _user.Id;
        f.FoodCapacity = capacity; f.FoodAmount = capacity;
        f.AddLog(LogType.Info, $"Feeder '{name}' created. Capacity {capacity}g");
        _user.Feeders.Add(f);
        return f;
    }

    public bool RemoveFeeder(Guid id) => _user.Feeders.RemoveAll(f => f.Id == id) > 0;

    public Feeder? Find(Guid id) => _user.Feeders.FirstOrDefault(f => f.Id == id);

    public FeedingResult Feed(Guid feederId, double grams)
    {
        var f = Find(feederId);
        if (f == null) return FeedingResult.NoFood;
        var r = f.Dispense(grams);
        if (r != FeedingResult.NoFood) TotalDispensed += grams;
        return r;
    }

    public void AssignSchedule(Guid feederId, Guid? scheduleId)
    {
        var f = Find(feederId);
        if (f != null) f.ScheduleId = scheduleId;
    }

    public void RefillWater(Guid feederId, double ml)
    {
        var f = Find(feederId);
        if (f != null) { f.WaterAmount += ml; f.AddLog(LogType.Info, $"Water refilled: {ml}ml"); }
    }

    // ---------- Schedule CRUD ----------
    public Schedule AddSchedule(string name)
    {
        var s = new Schedule { Name = name };
        _user.Schedules.Add(s);
        return s;
    }

    public bool RemoveSchedule(Guid id)
    {
        _user.Schedules.RemoveAll(x => x.Id == id);
        foreach (var f in _user.Feeders.Where(x => x.ScheduleId == id)) f.ScheduleId = null;
        return true;
    }

    public void AddFeeding(Guid scheduleId, TimeSpan time, double amount)
    {
        var s = _user.Schedules.FirstOrDefault(x => x.Id == scheduleId);
        if (s == null) return;
        s.Feedings.Add(new ScheduledFeeding { TimeOfDay = time, AmountGrams = amount });
        s.Feedings.Sort((a, b) => a.TimeOfDay.CompareTo(b.TimeOfDay));
    }

    public void RemoveFeeding(Guid scheduleId, Guid feedingId)
    {
        var s = _user.Schedules.FirstOrDefault(x => x.Id == scheduleId);
        s?.Feedings.RemoveAll(x => x.Id == feedingId);
    }

    // ---------- Postpone / cancel nearest ----------
    public static string StateKey(ScheduledFeeding sf, DateTime day) => $"{sf.Id}|{day:yyyyMMdd}";

    public (ScheduledFeeding sf, DateTime runAt)? FindNextFeeding(Feeder f, Schedule s, DateTime now)
    {
        var candidates = new List<(ScheduledFeeding, DateTime)>();
        for (int d = 0; d <= 1; d++)
        {
            var day = now.Date.AddDays(d);
            foreach (var sf in s.Feedings)
            {
                var key = StateKey(sf, day);
                var state = f.FeedingStates.ContainsKey(key) ? f.FeedingStates[key] : null;
                if (state?.ExecutedAt != null || state?.Cancelled == true) continue;
                var runAt = state?.PostponedUntil ?? (day + sf.TimeOfDay);
                if (runAt > now) candidates.Add((sf, runAt));
            }
        }
        return candidates.Count == 0 ? null : candidates.OrderBy(c => c.Item2).First();
    }

    public string? PostponeNearest(Guid feederId, DateTime now, TimeSpan delay)
    {
        var f = Find(feederId);
        var s = f?.ScheduleId is Guid sid ? _user.Schedules.FirstOrDefault(x => x.Id == sid) : null;
        if (f == null || s == null) return null;
        var next = FindNextFeeding(f, s, now);
        if (next == null) return "No upcoming feedings";
        var (sf, runAt) = next.Value;
        var day = runAt.Date;
        var key = StateKey(sf, day);
        if (!f.FeedingStates.ContainsKey(key)) f.FeedingStates.Add(key, new FeedingState());
        var newTime = now + delay;
        f.FeedingStates[key].PostponedUntil = newTime;
        f.AddLog(LogType.Info, $"Feeding postponed from {runAt:HH:mm} to {newTime:HH:mm}");
        return $"Postponed to {newTime:HH:mm:ss}";
    }

    public string? CancelNearest(Guid feederId, DateTime now)
    {
        var f = Find(feederId);
        var s = f?.ScheduleId is Guid sid ? _user.Schedules.FirstOrDefault(x => x.Id == sid) : null;
        if (f == null || s == null) return null;
        var next = FindNextFeeding(f, s, now);
        if (next == null) return "No upcoming feedings";
        var (sf, runAt) = next.Value;
        var key = StateKey(sf, runAt.Date);
        if (!f.FeedingStates.ContainsKey(key)) f.FeedingStates.Add(key, new FeedingState());
        f.FeedingStates[key].Cancelled = true;
        f.AddLog(LogType.Info, $"Feeding at {runAt:HH:mm} cancelled");
        return $"Cancelled feeding at {runAt:HH:mm}";
    }

    // ---------- Export / import ----------
    public void ExportFeederLog(Feeder f, string path)
    {
        var lines = f.Log.Select(l => $"[{l.Time:yyyy-MM-dd HH:mm:ss}] [{l.Type,-10}] {l.Message}");
        File.WriteAllLines(path, lines);
    }

    public void ExportSchedule(Guid scheduleId, string path)
    {
        var s = _user.Schedules.FirstOrDefault(x => x.Id == scheduleId);
        if (s == null) return;
        File.WriteAllText(path, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
    }

    public Schedule? ImportSchedule(string path)
    {
        var s = JsonSerializer.Deserialize<Schedule>(File.ReadAllText(path));
        if (s == null) return null;
        s.Id = Guid.NewGuid();
        foreach (var sf in s.Feedings) sf.Id = Guid.NewGuid();
        _user.Schedules.Add(s);
        return s;
    }
}