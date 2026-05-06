using SignFabric.Application.Abstractions;
using SignFabric.Infrastructure.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Logging {
	/// <summary>
	/// Audit logger implementation
	/// Can be extended to log to database, file system, or external service
	/// </summary>
	public class AuditLogger : IAuditLogger {
		private readonly AppSettingsPathResolver _paths;

		public AuditLogger(AppSettingsPathResolver paths) {
			_paths = paths;
		}

		public async Task LogEnvelopeCreatedAsync(string envelopeId, string userId) {
			await WriteAsync($"Envelope created: {envelopeId} by user {userId} at {DateTime.UtcNow:O}");
		}

		public async Task LogEnvelopeSentAsync(string envelopeId, string userId) {
			await WriteAsync($"Envelope sent: {envelopeId} by user {userId} at {DateTime.UtcNow:O}");
		}

		public async Task LogDocumentSignedAsync(string envelopeId, string signerId, DateTime timestamp) {
			await WriteAsync($"Document signed: {envelopeId} by signer {signerId} at {timestamp:O}");
		}

		public async Task LogEnvelopeCompletedAsync(string envelopeId, DateTime timestamp) {
			await WriteAsync($"Envelope completed: {envelopeId} at {timestamp:O}");
		}

		private async Task WriteAsync(string message) {
			var line = $"[AUDIT] {message}{Environment.NewLine}";
			System.Diagnostics.Debug.Write(line);
			await File.AppendAllTextAsync(Path.Combine(_paths.AuditLogsPath, $"{DateTime.UtcNow:yyyyMMdd}.log"), line);
		}
	}
}
