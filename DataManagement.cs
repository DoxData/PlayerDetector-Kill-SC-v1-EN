using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;

namespace PlayerDetector-Kill-SC-v1-EN
{
    // ...existing code...
    public class DataManagement
    {
        private readonly ILogger<DataManagement> _logger;
        private ConcurrentQueue<DeathEvent> deathHistory;
        private PerformanceSnapshot[] fpsData;
        private int currentIndex;

        public DataManagement(ConcurrentQueue<DeathEvent> deathHistory, PerformanceSnapshot[] fpsData, int currentIndex, ILogger<DataManagement> logger)
        {
            this.deathHistory = deathHistory;
            this.fpsData = fpsData;
            this.currentIndex = currentIndex;
            _logger = logger;
        }

        public void SaveDailyReport(string path)
        {
            var json = JsonConvert.SerializeObject(new
            {
                Deaths = deathHistory.Distinct().ToList(),
                Performance = fpsData.Take(currentIndex)
            }, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public void LogError(string error)
        {
            _logger.LogError(error);
            File.AppendAllText("errors.log", $"{DateTime.Now}: {error}\n");
        }

        public List<string> GetErrors()
        {
            return File.ReadAllLines("errors.log").Distinct().ToList();
        }
    }
}
// Copyright (c) 2024 DoxData. Exclusive ownership. 
// Prohibited use/modification without express authorization.
