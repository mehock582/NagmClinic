using System.Collections.ObjectModel;

namespace NagmClinic.Services.Laboratory.Connector
{
    public enum DeviceConnectionType
    {
        TcpIp = 1,
        SerialRs232 = 2,
        UsbVirtualCom = 3,
        UsbFile = 4,
        Wifi = 5,
        Bluetooth = 6
    }

    public sealed class DeviceConnectionOptions
    {
        public DeviceConnectionType ConnectionType { get; set; }

        public string DeviceId { get; set; } = string.Empty;

        public string? Host { get; set; }

        public int Port { get; set; }

        public string? SerialPortName { get; set; }

        public int BaudRate { get; set; } = 9600;

        public string? WatchPath { get; set; }

        public string? ConnectorSource { get; set; }
    }

    public interface IDeviceConnector
    {
        string Name { get; }

        IReadOnlyCollection<DeviceConnectionType> SupportedConnections { get; }

        Task ConnectAsync(DeviceConnectionOptions options, CancellationToken cancellationToken = default);

        IAsyncEnumerable<NormalizedLabResultItem> ReadNormalizedResultsAsync(CancellationToken cancellationToken = default);

        Task DisconnectAsync(CancellationToken cancellationToken = default);
    }

    public interface IDeviceDataChannel : IAsyncDisposable
    {
        Task OpenAsync(CancellationToken cancellationToken = default);

        IAsyncEnumerable<string> ReadFramesAsync(CancellationToken cancellationToken = default);
    }

    public interface IDeviceDataChannelFactory
    {
        IDeviceDataChannel Create(DeviceConnectionOptions options);
    }

    public sealed class DeviceConnectorFactoryMap
    {
        public DeviceConnectorFactoryMap(params IDeviceConnector[] connectors)
        {
            Connectors = new ReadOnlyDictionary<string, IDeviceConnector>(
                connectors.ToDictionary(c => c.Name.ToUpperInvariant(), c => c));
        }

        public IReadOnlyDictionary<string, IDeviceConnector> Connectors { get; }
    }
}
