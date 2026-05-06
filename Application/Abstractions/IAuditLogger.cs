using System;
using System.Threading.Tasks;

namespace SignFabric.Application.Abstractions {
	/// <summary>
	/// Abstraction for audit logging
	/// </summary>
	public interface IAuditLogger {
		Task LogEnvelopeCreatedAsync(string envelopeId, string userId);
		Task LogEnvelopeSentAsync(string envelopeId, string userId);
		Task LogDocumentSignedAsync(string envelopeId, string signerId, DateTime timestamp);
		Task LogEnvelopeCompletedAsync(string envelopeId, DateTime timestamp);
	}
}
