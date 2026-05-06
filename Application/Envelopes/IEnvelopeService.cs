using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Application.Envelopes {
	/// <summary>
	/// Application service for envelope (signing request) management
	/// </summary>
	public interface IEnvelopeService {
		Task<Envelope> CreateAsync(Envelope envelope, MemoryStream documentStream);
		Task<Envelope> GetAsync(string envelopeId);
		Task<List<Envelope>> GetAllAsync(string userId);
		Task UpdateAsync(Envelope envelope);
		Task SendAsync(string envelopeId);
		Task CompleteSigningAsync(string envelopeId);
		Task<byte[]> GetSignedDocumentAsync(string envelopeId);
	}
}
