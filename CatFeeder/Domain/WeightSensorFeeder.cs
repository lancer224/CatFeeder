using System;

namespace CatFeeder.Domain
{
    public class WeightSensorFeeder : Feeder
    {
        public override FeederType Type => FeederType.WeightSensor;
        public double FeedRateGramsPerSecond { get; set; }

        public WeightSensorFeeder() { FeedRateGramsPerSecond = 5; }

        public override FeedingResult Dispense(double targetBowlAmount)
        {
            if (targetBowlAmount <= 0) return FeedingResult.NoFood;
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
}
