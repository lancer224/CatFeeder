using CatFeeder.Domain;

namespace CatFeeder.ViewModels
{
    public class FeederRow
    {
        public Feeder Feeder { get; }
        public string ScheduleName { get; }

        public FeederRow(Feeder f, string schedName)
        {
            Feeder = f;
            ScheduleName = schedName;
        }

        public string Name => Feeder.Name;
        public string TypeName => Feeder.Type == FeederType.Dispenser ? "Дозатор" : "По весу";
        public string FoodText => $"{Feeder.FoodAmount:F1} / {Feeder.FoodCapacity:F0} г";
        public string BowlText => $"{Feeder.BowlAmount:F1} г";
        public string WaterText => $"{Feeder.WaterAmount:F0} мл";
        public string LabelsText => string.Join(", ", Feeder.Labels);
        public bool IsEmpty => Feeder.IsFoodEmpty;
    }
}
