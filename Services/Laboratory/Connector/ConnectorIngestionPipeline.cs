namespace NagmClinic.Services.Laboratory.Connector
{
    public interface IConnectorIngestionPipeline
    {
        Task StartListeningAsync(
            IDeviceConnector connector,
            DeviceConnectionOptions options,
            string connectorSource,
            CancellationToken cancellationToken = default);
    }

    public class ConnectorIngestionPipeline : IConnectorIngestionPipeline
    {
        private readonly IConnectorOutboxStore _outboxStore;
        private readonly ILogger<ConnectorIngestionPipeline> _logger;

        public ConnectorIngestionPipeline(
            IConnectorOutboxStore outboxStore,
            ILogger<ConnectorIngestionPipeline> logger)
        {
            _outboxStore = outboxStore;
            _logger = logger;
        }

        public async Task StartListeningAsync(
            IDeviceConnector connector,
            DeviceConnectionOptions options,
            string connectorSource,
            CancellationToken cancellationToken = default)
        {
            await connector.ConnectAsync(options, cancellationToken);

            await foreach (var normalized in connector.ReadNormalizedResultsAsync(cancellationToken))
            {
                _logger.LogInformation(
                    "Connector received reading | Device={DeviceId} Identifier={PatientIdentifier} Test={TestCode}",
                    normalized.DeviceId,
                    normalized.PatientIdentifier,
                    normalized.TestCode);

                await _outboxStore.EnqueueAsync(new ConnectorOutboxItem
                {
                    ConnectorSource = connectorSource,
                    Payload = normalized
                }, cancellationToken);
            }
        }
    }
}
