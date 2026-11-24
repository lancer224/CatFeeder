using System;
using System.Collections.Generic;

namespace CatFeeder.Domain
{
    public class User
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public List<Feeder> Feeders { get; set; }
        public List<Schedule> Schedules { get; set; }
        public List<Cat> Cats { get; set; }

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
