using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace NagmClinic.Services.Laboratory.Connector
{
    public class ConnectorDispatchOptions
    {
        public string QueueFilePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "connector-outbox.json");

        public int MaxRetryAttempts { get; set; } = 10;

        public int RetryBaseDelaySeconds { get; set; } = 5;

        public int BatchSize { get; set; } = 50;
    }

    public class ConnectorClinicApiOptions
    {
        public string ImportEndpoint { get; set; } = "https://localhost:5001/api/lab-results/import";

        public string ApiKeyHeaderName { get; set; } = "X-Connector-Api-Key";

        public string ApiKey { get; set; } = string.Empty;
    }

    public class ConnectorOutboxItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string ConnectorSource { get; set; } = string.Empty;

        public NormalizedLabResultItem Payload { get; set; } = new();

        public int AttemptCount { get; set; }

        public DateTime NextAttemptAtUtc { get; set; } = DateTime.UtcNow;

        public string? LastError { get; set; }
    }

    public interface IConnectorOutboxStore
    {
        Task EnqueueAsync(ConnectorOutboxItem item, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ConnectorOutboxItem>> GetDueItemsAsync(int maxCount, CancellationToken cancellationToken = default);

        Task MarkSucceededAsync(Guid id, CancellationToken cancellationToken = default);

        Task ScheduleRetryAsync(Guid id, int delaySeconds, string error, CancellationToken cancellationToken = default);
    }

    public class JsonFileConnectorOutboxStore : IConnectorOutboxStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly ConnectorDispatchOptions _options;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public JsonFileConnectorOutboxStore(IOptions<ConnectorDispatchOptions> options)
        {
            _options = options.Value;
        }

        public async Task EnqueueAsync(ConnectorOutboxItem item, CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                var items = await LoadUnsafeAsync(cancellationToken);
                items.Add(item);
                await SaveUnsafeAsync(items, cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<IReadOnlyList<ConnectorOutboxItem>> GetDueItemsAsync(int maxCount, CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                var items = await LoadUnsafeAsync(cancellationToken);
                var now = DateTime.UtcNow;
                return items
                    .Where(i => i.NextAttemptAtUtc <= now)
                    .OrderBy(i => i.NextAttemptAtUtc)
                    .ThenBy(i => i.AttemptCount)
                    .Take(maxCount)
                    .ToList();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task MarkSucceededAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                var items = await LoadUnsafeAsync(cancellationToken);
                items.RemoveAll(i => i.Id == id);
                await SaveUnsafeAsync(items, cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task ScheduleRetryAsync(Guid id, int delaySeconds, string error, CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                var items = await LoadUnsafeAsync(cancellationToken);
                var item = items.FirstOrDefault(i => i.Id == id);
                if (item == null)
                {
                    return;
                }

                item.AttemptCount++;
                item.LastError = error;
                item.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
                await SaveUnsafeAsync(items, cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<List<ConnectorOutboxItem>> LoadUnsafeAsync(CancellationToken cancellationToken)
        {
            EnsureDirectory();
            if (!File.Exists(_options.QueueFilePath))
            {
                return new List<ConnectorOutboxItem>();
            }

            var json = await File.ReadAllTextAsync(_options.QueueFilePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<ConnectorOutboxItem>();
            }

            return JsonSerializer.Deserialize<List<ConnectorOutboxItem>>(json, JsonOptions) ?? new List<ConnectorOutboxItem>();
        }

        private async Task SaveUnsafeAsync(List<ConnectorOutboxItem> items, CancellationToken cancellationToken)
        {
            EnsureDirectory();
            var json = JsonSerializer.Serialize(items, JsonOptions);
            await File.WriteAllTextAsync(_options.QueueFilePath, json, cancellationToken);
        }

        private void EnsureDirectory()
        {
            var directory = Path.GetDirectoryName(_options.QueueFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

    public interface IConnectorClinicApiClient
    {
        Task<(bool Success, string? Error)> SendAsync(ConnectorOutboxItem item, CancellationToken cancellationToken = default);
    }

    public class ConnectorClinicApiClient : IConnectorClinicApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ConnectorClinicApiOptions _options;

        public ConnectorClinicApiClient(HttpClient httpClient, IOptions<ConnectorClinicApiOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task<(bool Success, string? Error)> SendAsync(ConnectorOutboxItem item, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                return (false, "Connector API key is missing.");
            }

            var request = new LabResultsImportRequest
            {
                ConnectorSource = item.ConnectorSource,
                Results = new List<NormalizedLabResultItem> { item.Payload }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.ImportEndpoint)
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add(_options.ApiKeyHeaderName, _options.ApiKey);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return (false, $"{(int)response.StatusCode} {response.ReasonPhrase} | {content}");
        }
    }

    public class ConnectorResultDispatchService
    {
        private readonly IConnectorOutboxStore _outboxStore;
        private readonly IConnectorClinicApiClient _apiClient;
        private readonly ConnectorDispatchOptions _options;
        private readonly ILogger<ConnectorResultDispatchService> _logger;

        public ConnectorResultDispatchService(
            IConnectorOutboxStore outboxStore,
            IConnectorClinicApiClient apiClient,
            IOptions<ConnectorDispatchOptions> options,
            ILogger<ConnectorResultDispatchService> logger)
        {
            _outboxStore = outboxStore;
            _apiClient = apiClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task FlushOnceAsync(CancellationToken cancellationToken = default)
        {
            var dueItems = await _outboxStore.GetDueItemsAsync(_options.BatchSize, cancellationToken);
            foreach (var item in dueItems)
            {
                if (item.AttemptCount >= _options.MaxRetryAttempts)
                {
                    _logger.LogError(
                        "Connector outbox item {OutboxId} exceeded retry limit ({AttemptCount}). LastError: {LastError}",
                        item.Id,
                        item.AttemptCount,
                        item.LastError);
                    continue;
                }

                var (success, error) = await _apiClient.SendAsync(item, cancellationToken);
                if (success)
                {
                    await _outboxStore.MarkSucceededAsync(item.Id, cancellationToken);
                    _logger.LogInformation("Connector outbox item {OutboxId} delivered.", item.Id);
                    continue;
                }

                var backoff = CalculateDelaySeconds(item.AttemptCount);
                await _outboxStore.ScheduleRetryAsync(item.Id, backoff, error ?? "Unknown error", cancellationToken);
                _logger.LogWarning(
                    "Connector outbox item {OutboxId} failed. Retry in {Backoff}s. Error: {Error}",
                    item.Id,
                    backoff,
                    error);
            }
        }

        private int CalculateDelaySeconds(int attemptCount)
        {
            var baseDelay = Math.Max(1, _options.RetryBaseDelaySeconds);
            var exponent = Math.Min(8, Math.Max(0, attemptCount));
            return baseDelay * (int)Math.Pow(2, exponent);
        }
    }
}
