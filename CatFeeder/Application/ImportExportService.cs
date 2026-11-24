using CatFeeder.Domain;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CatFeeder.Application
{
    public class ImportExportService
    {
        private readonly User _user;

        public ImportExportService(User user)
        {
            _user = user;
        }

        public void ExportFeederLog(Feeder f, string path)
        {
            var lines = f.Log.Select(l => $"[{l.Time:yyyy-MM-dd HH:mm:ss}] [{l.Type,-10}] {l.Message}");
            File.WriteAllLines(path, lines);
        }

        public void ExportSchedule(Guid scheduleId, string path)
        {
            var s = _user.Schedules.FirstOrDefault(x => x.Id == scheduleId);
            if (s == null) return;
            File.WriteAllText(path, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }

        public Schedule ImportSchedule(string path)
        {
            var s = JsonSerializer.Deserialize<Schedule>(File.ReadAllText(path));
            if (s == null) return null;
            s.Id = Guid.NewGuid();
            foreach (var sf in s.Feedings) sf.Id = Guid.NewGuid();
            _user.Schedules.Add(s);
            return s;
        }
    }
}
