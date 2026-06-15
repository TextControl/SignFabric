using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SignFabric.Application.Services {
	public interface IEnvelopeWorkflowService {
		Task<Envelope> AddRecipientAsync(string userId, string envelopeId, Signer signer);
		Task<Envelope> GetRecipientsAsync(string userId, string envelopeId);
		Task<Envelope> RemoveRecipientAsync(string userId, string envelopeId, Signer signer);
		Task<Envelope> UpdateWorkflowAsync(string userId, string envelopeId, EnvelopeWorkflowUpdate request);
		Task<Envelope> UpdateAsync(string userId, Envelope envelope);
		Task<Envelope> SubmitAsync(string userId, string envelopeId, string host);
		Task<string> CreateAsync(string userId, string userName, MemoryStream documentStream, string fileName);
		Task<string> CreateAsync(string userId, string userName, MemoryStream documentStream, string fileName, string signingCertificateId);
	}

	public class EnvelopeWorkflowUpdate {
		public EnvelopeWorkflowMode WorkflowMode { get; set; }
		public List<EnvelopeRecipientWorkflowUpdate> Recipients { get; set; } = new List<EnvelopeRecipientWorkflowUpdate>();
	}

	public class EnvelopeRecipientWorkflowUpdate {
		public string Id { get; set; }
		public RecipientRole Role { get; set; }
		public int RoutingOrder { get; set; }
		public bool RequireEmailOtp { get; set; }
	}
}
