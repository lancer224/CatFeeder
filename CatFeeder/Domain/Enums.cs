using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatFeeder.Domain
{
    public enum FeederType { Dispenser, WeightSensor }
    public enum LogType { Info, Feeding, Warning, Error, Sensor, Simulation }
    public enum FeedingResult { Success, Partial, NoFood }
}
