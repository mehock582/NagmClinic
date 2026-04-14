using System.Runtime.CompilerServices;

namespace NagmClinic.Services.Laboratory.Connector
{
    public abstract class DeviceConnectorBase : IDeviceConnector
    {
        private readonly IDeviceDataChannelFactory _channelFactory;
        private readonly ILogger _logger;
        private DeviceConnectionOptions? _options;
        private IDeviceDataChannel? _channel;

        protected DeviceConnectorBase(IDeviceDataChannelFactory channelFactory, ILogger logger)
        {
            _channelFactory = channelFactory;
            _logger = logger;
        }

        public abstract string Name { get; }

        public abstract IReadOnlyCollection<DeviceConnectionType> SupportedConnections { get; }

        public async Task ConnectAsync(DeviceConnectionOptions options, CancellationToken cancellationToken = default)
        {
            if (!SupportedConnections.Contains(options.ConnectionType))
            {
                throw new NotSupportedException($"{Name} does not support {options.ConnectionType}");
            }

            _options = options;
            _channel = _channelFactory.Create(options);
            await _channel.OpenAsync(cancellationToken);

            _logger.LogInformation("Device connector {ConnectorName} connected with {ConnectionType}", Name, options.ConnectionType);
        }

        public async IAsyncEnumerable<NormalizedLabResultItem> ReadNormalizedResultsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_channel == null || _options == null)
            {
                yield break;
            }

            await foreach (var frame in _channel.ReadFramesAsync(cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(frame))
                {
                    continue;
                }

                foreach (var item in ParseFrame(frame, _options))
                {
                    if (item.Timestamp == default)
                    {
                        item.Timestamp = DateTime.UtcNow;
                    }

                    if (string.IsNullOrWhiteSpace(item.DeviceId))
                    {
                        item.DeviceId = _options.DeviceId;
                    }

                    item.RawPayload ??= frame;
                    yield return item;
                }
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_channel != null)
            {
                await _channel.DisposeAsync();
                _channel = null;
            }

            _logger.LogInformation("Device connector {ConnectorName} disconnected", Name);
        }

        protected abstract IEnumerable<NormalizedLabResultItem> ParseFrame(string frame, DeviceConnectionOptions options);

        protected static DateTime ParseTimestampOrNow(string? value)
        {
            return DateTime.TryParse(value, out var parsed)
                ? parsed
                : DateTime.UtcNow;
        }
    }

    public sealed class ECSeriesConnector : DeviceConnectorBase
    {
        private static readonly DeviceConnectionType[] Connections =
        {
            DeviceConnectionType.TcpIp,
            DeviceConnectionType.UsbVirtualCom,
            DeviceConnectionType.Wifi
        };

        public ECSeriesConnector(IDeviceDataChannelFactory channelFactory, ILogger<ECSeriesConnector> logger)
            : base(channelFactory, logger)
        {
        }

        public override string Name => "ECSeries";

        public override IReadOnlyCollection<DeviceConnectionType> SupportedConnections => Connections;

        protected override IEnumerable<NormalizedLabResultItem> ParseFrame(string frame, DeviceConnectionOptions options)
        {
            // EC style: "sample=24041001;test=WBC;value=6.2;unit=10^9/L;time=2026-04-10T12:01:00"
            var pairs = frame.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.Split('=', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0].Trim().ToUpperInvariant(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);

            if (!pairs.TryGetValue("SAMPLE", out var sampleId) ||
                !pairs.TryGetValue("TEST", out var testCode) ||
                !pairs.TryGetValue("VALUE", out var value))
            {
                yield break;
            }

            yield return new NormalizedLabResultItem
            {
                DeviceId = options.DeviceId,
                PatientIdentifier = sampleId,
                TestCode = testCode,
                ResultValue = value,
                Unit = pairs.TryGetValue("UNIT", out var unit) ? unit : null,
                Timestamp = ParseTimestampOrNow(pairs.TryGetValue("TIME", out var rawTime) ? rawTime : null)
            };
        }
    }

    public sealed class LansionbioConnector : DeviceConnectorBase
    {
        private static readonly DeviceConnectionType[] Connections =
        {
            DeviceConnectionType.SerialRs232,
            DeviceConnectionType.TcpIp,
            DeviceConnectionType.UsbVirtualCom,
            DeviceConnectionType.Wifi,
            DeviceConnectionType.Bluetooth
        };

        public LansionbioConnector(IDeviceDataChannelFactory channelFactory, ILogger<LansionbioConnector> logger)
            : base(channelFactory, logger)
        {
        }

        public override string Name => "Lansionbio";

        public override IReadOnlyCollection<DeviceConnectionType> SupportedConnections => Connections;

        protected override IEnumerable<NormalizedLabResultItem> ParseFrame(string frame, DeviceConnectionOptions options)
        {
            // Lansionbio style (CSV rows): "sampleId,testCode,result,unit,timestamp"
            var lines = frame.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines)
            {
                var columns = line.Split(',', StringSplitOptions.TrimEntries);
                if (columns.Length < 3)
                {
                    continue;
                }

                yield return new NormalizedLabResultItem
                {
                    DeviceId = options.DeviceId,
                    PatientIdentifier = columns[0],
                    TestCode = columns[1],
                    ResultValue = columns[2],
                    Unit = columns.Length > 3 ? columns[3] : null,
                    Timestamp = ParseTimestampOrNow(columns.Length > 4 ? columns[4] : null)
                };
            }
        }
    }

    public sealed class BioelabConnector : DeviceConnectorBase
    {
        private static readonly DeviceConnectionType[] Connections =
        {
            DeviceConnectionType.UsbFile,
            DeviceConnectionType.UsbVirtualCom
        };

        public BioelabConnector(IDeviceDataChannelFactory channelFactory, ILogger<BioelabConnector> logger)
            : base(channelFactory, logger)
        {
        }

        public override string Name => "Bioelab";

        public override IReadOnlyCollection<DeviceConnectionType> SupportedConnections => Connections;

        protected override IEnumerable<NormalizedLabResultItem> ParseFrame(string frame, DeviceConnectionOptions options)
        {
            // Bioelab LIS style: "sample|test|result|unit|timestamp"
            var lines = frame.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines)
            {
                var columns = line.Split('|', StringSplitOptions.TrimEntries);
                if (columns.Length < 3)
                {
                    continue;
                }

                yield return new NormalizedLabResultItem
                {
                    DeviceId = options.DeviceId,
                    PatientIdentifier = columns[0],
                    TestCode = columns[1],
                    ResultValue = columns[2],
                    Unit = columns.Length > 3 ? columns[3] : null,
                    Timestamp = ParseTimestampOrNow(columns.Length > 4 ? columns[4] : null)
                };
            }
        }
    }
}
