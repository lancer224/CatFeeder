using System;

namespace CatFeeder.Domain
{
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
            BowlAmount += actual;

            AddLog(LogType.Feeding, $"Выдано {actual:F1}г ({portions} порций). В миске: {BowlAmount:F1}г");
            var r = actual >= target ? FeedingResult.Success : FeedingResult.Partial;
            if (r == FeedingResult.Partial) AddLog(LogType.Warning, "Корм закончился во время выдачи");
            if (IsFoodEmpty) AddLog(LogType.Sensor, "Датчик: БАК ПУСТ");
            return r;
        }
    }
}
