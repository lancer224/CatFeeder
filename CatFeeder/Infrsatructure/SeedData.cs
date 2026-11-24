using CatFeeder.Application;
using CatFeeder.Domain;
using System;

namespace CatFeeder.Infrastructure
{
    public static class SeedData
    {
        public static void Initialize(FeederService fs)
        {
            var f1 = fs.AddFeeder(FeederType.Dispenser, "Кухня", 500);
            f1.Labels.AddRange(new[] { "Барсик", "кухня" });

            var f2 = fs.AddFeeder(FeederType.WeightSensor, "Загородный дом", 1000);
            f2.Labels.Add("дом");

            var dry = fs.AddSchedule("Сухой корм");
            fs.AddFeeding(dry.Id, TimeSpan.FromHours(8), 50);
            fs.AddFeeding(dry.Id, TimeSpan.FromHours(18), 50);
            fs.AssignSchedule(f1.Id, dry.Id);

            var cat1 = fs.AddCat("Барсик");
            cat1.AssignedFeederId = f1.Id;

            var cat2 = fs.AddCat("Мурка");
            cat2.AssignedFeederId = f2.Id;
        }
    }
}