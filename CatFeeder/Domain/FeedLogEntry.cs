using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatFeeder.Domain
{
    public class FeedLogEntry
    {
        public DateTime Time { get; set; }
        public LogType Type { get; set; }
        public string Message { get; set; }
    }
}
