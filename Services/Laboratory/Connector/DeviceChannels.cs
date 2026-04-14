using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace NagmClinic.Services.Laboratory.Connector
{
    public class DeviceDataChannelFactory : IDeviceDataChannelFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public DeviceDataChannelFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public IDeviceDataChannel Create(DeviceConnectionOptions options)
        {
            return options.ConnectionType switch
            {
                DeviceConnectionType.TcpIp or DeviceConnectionType.Wifi => new TcpDeviceChannel(options, _loggerFactory.CreateLogger<TcpDeviceChannel>()),
                DeviceConnectionType.UsbFile => new FilePollingDeviceChannel(options, _loggerFactory.CreateLogger<FilePollingDeviceChannel>()),
                DeviceConnectionType.SerialRs232 or DeviceConnectionType.UsbVirtualCom or DeviceConnectionType.Bluetooth => new SerialDeviceChannel(options, _loggerFactory.CreateLogger<SerialDeviceChannel>()),
                _ => throw new NotSupportedException($"Unsupported connection type: {options.ConnectionType}")
            };
        }
    }

    public sealed class TcpDeviceChannel : IDeviceDataChannel
    {
        private readonly DeviceConnectionOptions _options;
        private readonly ILogger<TcpDeviceChannel> _logger;
        private TcpClient? _client;
        private StreamReader? _reader;

        public TcpDeviceChannel(DeviceConnectionOptions options, ILogger<TcpDeviceChannel> logger)
        {
            _options = options;
            _logger = logger;
        }

        public async Task OpenAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.Host) || _options.Port <= 0)
            {
                throw new InvalidOperationException("TCP channel requires Host and Port.");
            }

            _client = new TcpClient();
            await _client.ConnectAsync(_options.Host, _options.Port, cancellationToken);
            _reader = new StreamReader(_client.GetStream(), Encoding.UTF8, leaveOpen: false);
            _logger.LogInformation("TCP channel connected for {DeviceId} at {Host}:{Port}", _options.DeviceId, _options.Host, _options.Port);
        }

        public async IAsyncEnumerable<string> ReadFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_reader == null)
            {
                yield break;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(cancellationToken);
                if (line == null)
                {
                    yield break;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    yield return line;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_reader != null)
            {
                _reader.Dispose();
            }

            _client?.Close();
            _client?.Dispose();
        }
    }

    public sealed class SerialDeviceChannel : IDeviceDataChannel
    {
        private readonly DeviceConnectionOptions _options;
        private readonly ILogger<SerialDeviceChannel> _logger;

        public SerialDeviceChannel(DeviceConnectionOptions options, ILogger<SerialDeviceChannel> logger)
        {
            _options = options;
            _logger = logger;
        }

        public Task OpenAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.SerialPortName))
            {
                throw new InvalidOperationException("Serial channel requires SerialPortName.");
            }

            _logger.LogInformation(
                "Serial channel initialized for {DeviceId} at {PortName} ({BaudRate} baud).",
                _options.DeviceId,
                _options.SerialPortName,
                _options.BaudRate);

            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<string> ReadFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // In this web project we keep serial support abstract.
            // Desktop/service host should provide concrete COM-port readers and feed this channel.
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }

            yield break;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    public sealed class FilePollingDeviceChannel : IDeviceDataChannel
    {
        private readonly DeviceConnectionOptions _options;
        private readonly ILogger<FilePollingDeviceChannel> _logger;
        private readonly HashSet<string> _processedFiles = new(StringComparer.OrdinalIgnoreCase);

        public FilePollingDeviceChannel(DeviceConnectionOptions options, ILogger<FilePollingDeviceChannel> logger)
        {
            _options = options;
            _logger = logger;
        }

        public Task OpenAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.WatchPath))
            {
                throw new InvalidOperationException("File channel requires WatchPath.");
            }

            if (!Directory.Exists(_options.WatchPath))
            {
                Directory.CreateDirectory(_options.WatchPath);
            }

            _logger.LogInformation("File polling channel started for {DeviceId} at {Path}", _options.DeviceId, _options.WatchPath);
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<string> ReadFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var files = Directory.EnumerateFiles(_options.WatchPath!, "*.txt")
                    .OrderBy(path => path)
                    .ToList();

                foreach (var file in files)
                {
                    if (_processedFiles.Contains(file))
                    {
                        continue;
                    }

                    var content = await File.ReadAllTextAsync(file, cancellationToken);
                    _processedFiles.Add(file);

                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        yield return content;
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
