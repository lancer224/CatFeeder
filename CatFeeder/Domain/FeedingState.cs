using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatFeeder.Domain
{
    public class FeedingState
    {
        public DateTime? PostponedUntil { get; set; }
        public bool Cancelled { get; set; }
        public DateTime? ExecutedAt { get; set; }
    }
}
