using CatFeeder.Domain;

namespace CatFeeder.ViewModels
{
    public class FeedingRow
    {
        public ScheduledFeeding Feeding { get; }
        public FeedingRow(ScheduledFeeding sf) => Feeding = sf;

        public string TimeText => Feeding.TimeOfDay.ToString(@"hh\:mm");
        public string AmountText => $"{Feeding.AmountGrams} г";
    }
}