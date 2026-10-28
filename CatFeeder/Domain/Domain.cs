using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace CatFeeder.Domain
{
    public enum FeederType { Dispenser, WeightSensor }
    public enum LogType { Info, Feeding, Warning, Error, Sensor, Simulation }
    public enum FeedingResult { Success, Partial, NoFood }

    public class FeedLogEntry
    {
        public DateTime Time { get; set; }
        public LogType Type { get; set; }
        public string Message { get; set; }
    }

    public class ScheduledFeeding
    {
        public Guid Id { get; set; }
        public TimeSpan TimeOfDay { get; set; }
        public double AmountGrams { get; set; }

        public ScheduledFeeding() { Id = Guid.NewGuid(); }
    }

    public class Schedule
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public List<ScheduledFeeding> Feedings { get; set; }

        public Schedule() { Id = Guid.NewGuid(); Feedings = new List<ScheduledFeeding>(); }
    }

    public class FeedingState
    {
        public DateTime? PostponedUntil { get; set; }
        public bool Cancelled { get; set; }
        public DateTime? ExecutedAt { get; set; }
    }

    public class Cat
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public double Hunger { get; set; } // 0..100
        public double EatThreshold { get; set; }
        public double WaterConsumptionRate { get; set; } // мл воды за 1 грамм корма
        public Guid? AssignedFeederId { get; set; }

        public Cat()
        {
            Id = Guid.NewGuid();
            Name = "Кот";
            Hunger = 30;
            EatThreshold = 60;
            WaterConsumptionRate = 1.5;
        }
    }

    public abstract class Feeder
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public List<string> Labels { get; set; }
        public double FoodCapacity { get; set; }
        public double FoodAmount { get; set; }
        public double WaterAmount { get; set; }

        // НОВОЕ: Корм, который уже насыпан в миску
        public double BowlAmount { get; set; }
        public Guid? ScheduleId { get; set; }

        [JsonIgnore] public List<FeedLogEntry> Log { get; set; }
        [JsonIgnore] public Dictionary<string, FeedingState> FeedingStates { get; set; }
        [JsonIgnore] public abstract FeederType Type { get; }

        protected Feeder()
        {
            Id = Guid.NewGuid();
            Labels = new List<string>();
            Log = new List<FeedLogEntry>();
            FeedingStates = new Dictionary<string, FeedingState>();
            BowlAmount = 0;
        }

        public bool IsFoodEmpty => FoodAmount <= 5;

        public void Refill(double grams)
        {
            FoodAmount = Math.Min(FoodCapacity, FoodAmount + grams);
            AddLog(LogType.Info, $"Бак пополнен на {grams}г, теперь {FoodAmount:F1}г");
        }

        public void CatEat(double grams)
        {
            double actual = Math.Min(grams, BowlAmount);
            BowlAmount -= actual;
            AddLog(LogType.Info, $"Кот съел {actual:F1}г из миски");
        }

        public abstract FeedingResult Dispense(double amount);

        public void AddLog(LogType type, string msg)
        {
            Log.Add(new FeedLogEntry { Time = DateTime.Now, Type = type, Message = msg });
            if (Log.Count > 5000) Log.RemoveAt(0);
        }
    }

    public class DispenserFeeder : Feeder
    {
        public override FeederType Type => FeederType.Dispenser;
        public double PortionSize { get; set; }

        public DispenserFeeder() { PortionSize = 25; }

        public override FeedingResult Dispense(double grams)
        {
            if (FoodAmount <= 0) { AddLog(LogType.Warning, "Нет корма в баке"); return FeedingResult.NoFood; }
            double portions = Math.Max(1, Math.Round(grams / PortionSize));
            double target = portions * PortionSize;
            double actual = Math.Min(target, FoodAmount);

            FoodAmount -= actual;
            BowlAmount += actual; // Насыпаем в миску

            AddLog(LogType.Feeding, $"Выдано {actual:F1}г ({portions} порций). В миске: {BowlAmount:F1}г");
            var r = actual >= target ? FeedingResult.Success : FeedingResult.Partial;
            if (r == FeedingResult.Partial) AddLog(LogType.Warning, "Корм закончился во время выдачи");
            if (IsFoodEmpty) AddLog(LogType.Sensor, "Датчик: БАК ПУСТ");
            return r;
        }
    }

    public class WeightSensorFeeder : Feeder
    {
        public override FeederType Type => FeederType.WeightSensor;
        public double FeedRateGramsPerSecond { get; set; }

        public WeightSensorFeeder() { FeedRateGramsPerSecond = 5; }

        public override FeedingResult Dispense(double targetBowlAmount)
        {
            if (targetBowlAmount <= 0) return FeedingResult.NoFood;

            // Для винтовой кормушки amount - это ЦЕЛЕВОЙ вес в миске
            double needed = targetBowlAmount - BowlAmount;

            if (needed <= 0.1)
            {
                AddLog(LogType.Info, $"В миске уже достаточно корма ({BowlAmount:F1}г). Выдача не требуется.");
                return FeedingResult.Success;
            }

            if (FoodAmount <= 0) { AddLog(LogType.Warning, "Нет корма в баке"); return FeedingResult.NoFood; }

            double actual = Math.Min(needed, FoodAmount);
            FoodAmount -= actual;
            BowlAmount += actual;

            AddLog(LogType.Feeding, $"Мотор крутится, досыпано {actual:F1}г (цель {targetBowlAmount:F1}г). В миске: {BowlAmount:F1}г");
            var r = actual >= needed - 0.1 ? FeedingResult.Success : FeedingResult.Partial;
            if (r == FeedingResult.Partial) AddLog(LogType.Warning, "Корм закончился во время выдачи");
            if (IsFoodEmpty) AddLog(LogType.Sensor, "Датчик веса: БАК ПУСТ");
            return r;
        }
    }

    public class User
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public List<Feeder> Feeders { get; set; }
        public List<Schedule> Schedules { get; set; }
        public List<Cat> Cats { get; set; } // НОВОЕ: Список котов

        public User()
        {
            Id = Guid.NewGuid();
            Name = "Owner";
            Feeders = new List<Feeder>();
            Schedules = new List<Schedule>();
            Cats = new List<Cat>();
        }
    }
}