using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatFeeder.Domain

{
    public class Cat
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public double Hunger { get; set; }
        public double EatThreshold { get; set; }
        public double WaterConsumptionRate { get; set; }
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
}

