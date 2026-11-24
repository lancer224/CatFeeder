using CatFeeder.Domain;

namespace CatFeeder.ViewModels
{
    public class CatRow
    {
        public Cat Cat { get; }
        public string FeederName { get; }

        public CatRow(Cat c, string feederName)
        {
            Cat = c;
            FeederName = feederName;
        }

        public string Name => Cat.Name;
        public string HungerText => $"{Cat.Hunger:F0} / 100";
        public string ThresholdText => $"{Cat.EatThreshold:F0}";
        public string WaterRateText => $"{Cat.WaterConsumptionRate:F1} мл";
    }
}
