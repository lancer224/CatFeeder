using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatFeeder.Domain
{
    public class Schedule
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public List<ScheduledFeeding> Feedings { get; set; }

        public Schedule() { Id = Guid.NewGuid(); Feedings = new List<ScheduledFeeding>(); }
    }
}
