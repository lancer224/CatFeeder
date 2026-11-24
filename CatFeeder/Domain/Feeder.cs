using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CatFeeder.Domain
{
    public abstract class Feeder
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public List<string> Labels { get; set; }
        public double FoodCapacity { get; set; }
        public double FoodAmount { get; set; }
        public double WaterAmount { get; set; }
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
}
