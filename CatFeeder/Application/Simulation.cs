using CatFeeder.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CatFeeder.Application
{
    public class SimulationService
    {
        private readonly FeederService _feederService;
        public DateTime SimulatedNow { get; private set; }
        public double TimeMultiplier { get; set; }
        public bool IsRunning { get; private set; }

        public event Action Tick;
        public event Action<string> CatEvent;

        private CancellationTokenSource _cts;
        private Task _task;
        private DateTime _lastSimTime;

        public SimulationService(FeederService fs)
        {
            _feederService = fs;
            SimulatedNow = DateTime.Now;
            _lastSimTime = DateTime.Now;
            TimeMultiplier = 720; // 1 час за 5 секунд
            IsRunning = false;
        }

        public void Start()
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            IsRunning = true;
            _lastSimTime = SimulatedNow;
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
            while (!token.IsCancellationRequested)
            {
                try { await Task.Delay(100, token); } catch (TaskCanceledException) { break; }
                var now = DateTime.UtcNow;
                var deltaReal = now - lastReal;
                lastReal = now;
                SimulatedNow = SimulatedNow.AddSeconds(deltaReal.TotalSeconds * TimeMultiplier);

                ProcessSchedules();
                ProcessCats();
                Tick?.Invoke();
            }
        }

        private void ProcessSchedules()
        {
            foreach (var f in _feederService.Feeders.ToList())
            {
                if (f.ScheduleId == null) continue;
                var s = _feederService.Schedules.FirstOrDefault(x => x.Id == f.ScheduleId.Value);
                if (s == null) continue;

                for (int d = -1; d <= 0; d++)
                {
                    var day = SimulatedNow.Date.AddDays(d);
                    foreach (var sf in s.Feedings)
                    {
                        var key = FeederService.StateKey(sf, day);
                        if (!f.FeedingStates.ContainsKey(key))
                            f.FeedingStates[key] = new FeedingState();

                        var state = f.FeedingStates[key];
                        if (state.Cancelled || state.ExecutedAt != null) continue;
                        var runAt = state.PostponedUntil ?? (day + sf.TimeOfDay);

                        if (SimulatedNow >= runAt)
                        {
                            state.ExecutedAt = SimulatedNow;
                            var r = _feederService.Feed(f.Id, sf.AmountGrams);
                            f.AddLog(LogType.Simulation, $"По расписанию в {SimulatedNow:HH:mm:ss} -> {r}");
                        }
                    }
                }
            }
        }

        private void ProcessCats()
        {
            var hours = (SimulatedNow - _lastSimTime).TotalHours;
            if (hours <= 0) return;
            _lastSimTime = SimulatedNow;

            foreach (var cat in _feederService.Cats)
            {
                cat.Hunger = Math.Min(100, cat.Hunger + hours * 8); // Голодает со скоростью 8 в час

                if (cat.Hunger < cat.EatThreshold) continue;

                Feeder f = null;

                // Если у кота есть привязка к конкретной кормушке
                if (cat.AssignedFeederId != null)
                {
                    f = _feederService.Find(cat.AssignedFeederId.Value);
                    if (f == null)
                    {
                        CatEvent?.Invoke($"[Кот {cat.Name}] мяукает: Моя кормушка не найдена! (голод {cat.Hunger:F0})");
                        continue;
                    }
                    if (f.BowlAmount <= 0)
                    {
                        CatEvent?.Invoke($"[Кот {cat.Name}] мяукает: Моя кормушка '{f.Name}' пуста! (голод {cat.Hunger:F0})");
                        continue;
                    }
                }
                else
                {
                    // Если привязки нет (выбрано "<Любая кормушка>"), ищем любую с едой
                    f = _feederService.Feeders.FirstOrDefault(x => x.BowlAmount > 0);
                    if (f == null)
                    {
                        CatEvent?.Invoke($"[Кот {cat.Name}] мяукает: ВЕЗДЕ ПУСТО! (голод {cat.Hunger:F0})");
                        continue;
                    }
                }

                // Кот ест из своей миски
                double eatAmount = Math.Min(30, f.BowlAmount); // Съедает до 30г за раз
                f.CatEat(eatAmount);
                cat.Hunger = Math.Max(0, cat.Hunger - eatAmount * 1.5); // Наедается

                // Тратим воду
                double waterNeeded = eatAmount * cat.WaterConsumptionRate;
                double actualWater = Math.Min(waterNeeded, f.WaterAmount);
                f.WaterAmount -= actualWater;

                string waterMsg = actualWater < waterNeeded ? " (ВОДЫ НЕ ХВАТИЛО!)" : "";
                CatEvent?.Invoke($"[Кот {cat.Name}] съел {eatAmount:F1}г из '{f.Name}', выпил {actualWater:F1}мл воды{waterMsg}. Голод: {cat.Hunger:F0}");
            }
        }
    }
}