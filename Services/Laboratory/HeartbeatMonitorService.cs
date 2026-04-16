using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NagmClinic.Services.Laboratory;

namespace NagmClinic.Services.Laboratory
{
    public class HeartbeatStore
    {
        private readonly ConcurrentDictionary<string, HeartbeatPayload> _heartbeats = new(StringComparer.OrdinalIgnoreCase);

        public void UpdateHeartbeat(HeartbeatPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.ConnectorSource)) return;
            _heartbeats[payload.ConnectorSource] = payload;
        }

        public IReadOnlyDictionary<string, HeartbeatPayload> GetAll()
        {
            return _heartbeats;
        }
    }

    public class HeartbeatMonitorService : BackgroundService
    {
        private readonly HeartbeatStore _store;
        private readonly ILogger<HeartbeatMonitorService> _logger;

        public HeartbeatMonitorService(HeartbeatStore store, ILogger<HeartbeatMonitorService> logger)
        {
            _store = store;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                
                try
                {
                    var currentUtc = DateTime.UtcNow;
                    var heartbeats = _store.GetAll();

                    foreach (var kvp in heartbeats)
                    {
                        var payload = kvp.Value;
                        var timeSinceLastHeartbeat = currentUtc - payload.Timestamp.ToUniversalTime();

                        if (timeSinceLastHeartbeat.TotalMinutes > 15)
                        {
                            _logger.LogCritical("CRITICAL ALERT: Lab Connector '{Source}' has not checked in for {Minutes:N1} minutes. Last heartbeat: {LastSeen}",
                                payload.ConnectorSource, timeSinceLastHeartbeat.TotalMinutes, payload.Timestamp);
                        }
                        else if (payload.QueueSize > 50)
                        {
                            _logger.LogCritical("CRITICAL ALERT: Lab Connector '{Source}' processing queue is backlogged. Current Queue Size: {QueueSize}",
                                payload.ConnectorSource, payload.QueueSize);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in HeartbeatMonitorService.");
                }
            }
        }
    }
}
