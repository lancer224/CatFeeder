using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatFeeder.Domain
{
    public class ScheduledFeeding
    {
        public Guid Id { get; set; }
        public TimeSpan TimeOfDay { get; set; }
        public double AmountGrams { get; set; }

        public ScheduledFeeding() { Id = Guid.NewGuid(); }
    }
}
