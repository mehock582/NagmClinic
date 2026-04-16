using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NagmClinic.Models.Configuration;
using NagmClinic.Services.Laboratory;

namespace NagmClinic.Controllers
{
    [ApiController]
    [Route("api/lab-results")]
    public class LabResultsImportController : ControllerBase
    {
        private readonly ILabResultImportService _importService;
        private readonly LabConnectorApiOptions _connectorOptions;
        private readonly ILogger<LabResultsImportController> _logger;
        private readonly IWebHostEnvironment _environment;

        public LabResultsImportController(
            ILabResultImportService importService,
            IOptions<LabConnectorApiOptions> connectorOptions,
            ILogger<LabResultsImportController> logger,
            IWebHostEnvironment environment)
        {
            _importService = importService;
            _connectorOptions = connectorOptions.Value;
            _logger = logger;
            _environment = environment;
        }

        [HttpPost("import")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Import([FromBody] LabResultsImportRequest request, CancellationToken cancellationToken)
        {
            if (!Request.IsHttps && !_connectorOptions.AllowHttpInDevelopment)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "HTTPS is required for lab imports."
                });
            }

            if (!IsAuthorized(Request))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid connector API key."
                });
            }

            if (request.Results == null || request.Results.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Payload must include at least one normalized result."
                });
            }

            var connectorSource = request.ConnectorSource;
            if (string.IsNullOrWhiteSpace(connectorSource) &&
                Request.Headers.TryGetValue("X-Connector-Source", out var sourceHeader))
            {
                connectorSource = sourceHeader.ToString();
            }

            request.ConnectorSource = connectorSource ?? string.Empty;
            var response = await _importService.ImportAsync(request, cancellationToken);

            _logger.LogInformation(
                "Lab import batch processed | Total={Total} Imported={Imported} Duplicates={Duplicates} Rejected={Rejected}",
                response.Total,
                response.Imported,
                response.Duplicates,
                response.Rejected);

            return Ok(new
            {
                success = true,
                result = response
            });
        }

        [HttpPost("heartbeat")]
        [IgnoreAntiforgeryToken]
        public IActionResult Heartbeat([FromBody] HeartbeatPayload payload, [FromServices] HeartbeatStore heartbeatStore)
        {
            if (!Request.IsHttps && !_connectorOptions.AllowHttpInDevelopment)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "HTTPS is required." });
            }

            if (!IsAuthorized(Request))
            {
                return Unauthorized(new { success = false, message = "Invalid connector API key." });
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.ConnectorSource))
            {
                return BadRequest(new { success = false, message = "Invalid payload." });
            }

            heartbeatStore.UpdateHeartbeat(payload);

            return Ok(new { success = true });
        }

        private bool IsAuthorized(HttpRequest request)
        {
            if (_environment.IsDevelopment() && _connectorOptions.AllowAnonymousInDevelopment)
            {
                _logger.LogWarning("Lab import request accepted without API key because AllowAnonymousInDevelopment=true.");
                return true;
            }

            var expectedKey = _connectorOptions.ApiKey?.Trim();
            if (string.IsNullOrWhiteSpace(expectedKey))
            {
                _logger.LogWarning("LabConnectorApi:ApiKey is not configured. Import endpoint will reject requests.");
                return false;
            }

            var headerName = string.IsNullOrWhiteSpace(_connectorOptions.ApiKeyHeaderName)
                ? "X-Connector-Api-Key"
                : _connectorOptions.ApiKeyHeaderName;

            if (!request.Headers.TryGetValue(headerName, out var receivedValue))
            {
                return false;
            }

            return string.Equals(receivedValue.ToString().Trim(), expectedKey, StringComparison.Ordinal);
        }
    }
}
