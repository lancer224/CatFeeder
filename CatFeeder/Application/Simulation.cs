using CatFeeder.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CatFeeder.Application;

public class SimulationService
{
    private readonly FeederService _feederService;
    public CatSimulator Cat { get; }
    public DateTime SimulatedNow { get; private set; } = DateTime.Now;
    public double TimeMultiplier { get; set; } = 720; // 1 час симуляции = 5 сек реального времени
    public bool IsRunning { get; private set; }

    public event Action? Tick;
    public event Action<string>? CatEvent;

    private CancellationTokenSource? _cts;
    private Task? _task;

    public SimulationService(FeederService fs, CatSimulator cat)
    {
        _feederService = fs;
        Cat = cat;
        Cat.OnEvent += msg => CatEvent?.Invoke(msg);
    }

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        IsRunning = true;
        _task = Task.Run(() => RunLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _task?.Wait(1500); } catch { }
        IsRunning = false;
    }

    private async Task RunLoop(CancellationToken token)
    {
        var lastReal = DateTime.UtcNow;
        Cat.ResetClock(SimulatedNow);
        while (!token.IsCancellationRequested)
        {
            try { await Task.Delay(100, token); } catch (TaskCanceledException) { break; }
            var now = DateTime.UtcNow;
            var deltaReal = now - lastReal;
            lastReal = now;
            SimulatedNow = SimulatedNow.AddSeconds(deltaReal.TotalSeconds * TimeMultiplier);
            ProcessSchedules();
            Cat.Tick(SimulatedNow, _feederService.Feeders);
            Tick?.Invoke();
        }
    }

    private void ProcessSchedules()
    {
        foreach (var f in _feederService.Feeders.ToList())
        {
            if (f.ScheduleId is not Guid sid) continue;
            var s = _feederService.Schedules.FirstOrDefault(x => x.Id == sid);
            if (s == null) continue;

            for (int d = -1; d <= 0; d++)
            {
                var day = SimulatedNow.Date.AddDays(d);
                foreach (var sf in s.Feedings)
                {
                    var key = FeederService.StateKey(sf, day);
                    if (!f.FeedingStates.TryGetValue(key, out var state))
                    {
                        state = new FeedingState();
                        f.FeedingStates[key] = state;
                    }
                    if (state.Cancelled || state.ExecutedAt != null) continue;
                    var runAt = state.PostponedUntil ?? (day + sf.TimeOfDay);
                    if (SimulatedNow >= runAt)
                    {
                        state.ExecutedAt = SimulatedNow;
                        var r = _feederService.Feed(f.Id, sf.AmountGrams);
                        f.AddLog(LogType.Simulation, $"Scheduled feed at {SimulatedNow:HH:mm:ss} -> {r}");
                    }
                }
            }
        }
    }
}

public class CatSimulator
{
    public string Name { get; set; } = "Barsik";
    public double Hunger { get; set; } = 30;              // 0..100
    public double EatThreshold { get; set; } = 60;
    public double HungerRatePerHour { get; set; } = 8;

    private DateTime _lastTick = DateTime.Now;
    public event Action<string>? OnEvent;

    public void ResetClock(DateTime simNow) => _lastTick = simNow;

    public void Tick(DateTime simNow, IEnumerable<Feeder> feeders)
    {
        var hours = (simNow - _lastTick).TotalHours;
        if (hours <= 0) return;
        _lastTick = simNow;
        Hunger = Math.Min(100, Hunger + hours * HungerRatePerHour);

        if (Hunger < EatThreshold) return;

        var withFood = feeders.Where(f => !f.IsFoodEmpty).ToList();
        if (withFood.Count == 0)
        {
            OnEvent?.Invoke($"[Cat] {Name} meows: NO FOOD anywhere! (hunger {Hunger:F0})");
            return;
        }
        var f = withFood[new Random().Next(withFood.Count)];
        double eat = Math.Min(30, f.FoodAmount);
        f.CatEat(eat);
        Hunger = Math.Max(0, Hunger - eat);
        OnEvent?.Invoke($"[Cat] {Name} ate {eat:F1}g from '{f.Name}'. Hunger: {Hunger:F0}");
    }
}