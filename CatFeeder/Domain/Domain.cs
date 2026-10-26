using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CatFeeder.Domain;

public enum FeederType { Dispenser, WeightSensor }
public enum LogType { Info, Feeding, Warning, Error, Sensor, Simulation }
public enum FeedingResult { Success, Partial, NoFood }

public class FeedLogEntry
{
    public DateTime Time { get; set; }
    public LogType Type { get; set; }
    public string Message { get; set; } = "";
}

public class ScheduledFeeding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TimeSpan TimeOfDay { get; set; }
    public double AmountGrams { get; set; }
}

public class Schedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public List<ScheduledFeeding> Feedings { get; set; } = new();
}

public class FeedingState
{
    public DateTime? PostponedUntil { get; set; }
    public bool Cancelled { get; set; }
    public DateTime? ExecutedAt { get; set; }
}

public abstract class Feeder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public List<string> Labels { get; set; } = new();
    public double FoodCapacity { get; set; } = 1000;
    public double FoodAmount { get; set; }
    public double WaterAmount { get; set; }
    public Guid? ScheduleId { get; set; }

    [JsonIgnore] public List<FeedLogEntry> Log { get; set; } = new();
    [JsonIgnore] public Dictionary<string, FeedingState> FeedingStates { get; set; } = new();
    [JsonIgnore] public abstract FeederType Type { get; }

    public bool IsFoodEmpty => FoodAmount <= 5;

    public void Refill(double grams)
    {
        FoodAmount = Math.Min(FoodCapacity, FoodAmount + grams);
        AddLog(LogType.Info, $"Refilled {grams}g, now {FoodAmount:F1}g");
    }

    public void CatEat(double grams)
    {
        double actual = Math.Min(grams, FoodAmount);
        FoodAmount -= actual;
        AddLog(LogType.Info, $"Cat ate {actual:F1}g");
    }

    public abstract FeedingResult Dispense(double grams);

    public void AddLog(LogType type, string msg)
    {
        Log.Add(new FeedLogEntry { Time = DateTime.Now, Type = type, Message = msg });
        if (Log.Count > 5000) Log.RemoveAt(0);
    }
}

public class DispenserFeeder : Feeder
{
    public override FeederType Type => FeederType.Dispenser;
    public double PortionSize { get; set; } = 25;

    public override FeedingResult Dispense(double grams)
    {
        if (FoodAmount <= 0) { AddLog(LogType.Warning, "No food to dispense"); return FeedingResult.NoFood; }
        double portions = Math.Max(1, Math.Round(grams / PortionSize));
        double target = portions * PortionSize;
        double actual = Math.Min(target, FoodAmount);
        FoodAmount -= actual;
        AddLog(LogType.Feeding, $"Dispensed {actual:F1}g ({portions} portions)");
        var r = actual >= target ? FeedingResult.Success : FeedingResult.Partial;
        if (r == FeedingResult.Partial) AddLog(LogType.Warning, "Ran low on food during dispense");
        if (IsFoodEmpty) AddLog(LogType.Sensor, "Food sensor: EMPTY");
        return r;
    }
}

public class WeightSensorFeeder : Feeder
{
    public override FeederType Type => FeederType.WeightSensor;
    public double FeedRateGramsPerSecond { get; set; } = 5;

    public override FeedingResult Dispense(double grams)
    {
        if (grams <= 0) return FeedingResult.NoFood;
        if (FoodAmount <= 0) { AddLog(LogType.Warning, "No food to dispense"); return FeedingResult.NoFood; }
        double actual = Math.Min(grams, FoodAmount);
        FoodAmount -= actual;
        AddLog(LogType.Feeding, $"Motor spun, dispensed {actual:F1}g (target {grams:F1}g)");
        var r = actual >= grams - 0.1 ? FeedingResult.Success : FeedingResult.Partial;
        if (r == FeedingResult.Partial) AddLog(LogType.Warning, "Ran out during dispense");
        if (IsFoodEmpty) AddLog(LogType.Sensor, "Weight sensor: EMPTY");
        return r;
    }
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Owner";
    public List<Feeder> Feeders { get; set; } = new();
    public List<Schedule> Schedules { get; set; } = new();
}