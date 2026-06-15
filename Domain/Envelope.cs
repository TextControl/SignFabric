using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Domain {
	public class Envelope {
		public int Id { get; set; }
		public string EnvelopeID { get; set; } 
		public string UserID { get; set; }
		public string Sender { get; set; }
		public string Name { get; set; }
		public DateTime Created { get; set; }
		public DateTime Sent { get; set; }
		public string SigningCertificateId { get; set; }
		public SigningCertificateEvidence SigningCertificate { get; set; }
		public List<Signer> Signers { get; set; } = new List<Signer>();
		public EnvelopeWorkflowMode WorkflowMode { get; set; }
		public EnvelopeStatus Status { get; set; }
		public string FaultMessage { get; set; }
		public bool ContainsSignatureBoxes { get; set; }
		public SignatureModel SignatureInformation { get; set; }
		public DateTime? FinalizedAt { get; set; }
		public string FinalDocumentHashSha256 { get; set; }
		public string FinalDocumentHashMD5 { get; set; }
		public long? FinalDocumentSizeBytes { get; set; }
		public string OriginalDocumentHashSha256 { get; set; }
		public string ValidationId { get; set; }
	}

	public class SigningCertificateEvidence {
		public string RecordId { get; set; }
		public string DisplayName { get; set; }
		public string Thumbprint { get; set; }
		public string Subject { get; set; }
		public string Issuer { get; set; }
		public string NotBefore { get; set; }
		public string NotAfter { get; set; }
		public string Provider { get; set; }
		public DateTime CapturedAt { get; set; }
	}

	public class Signer {
		private SignerStatus m_signerStatus;

		public string Id { get; set; }
		public string Name { get; set; }
		public string Email { get; set; }
		public RecipientRole Role { get; set; }
		public int RoutingOrder { get; set; } = 1;
		public bool RoutingActive { get; set; }
		public DateTime? RoutingActivatedAt { get; set; }
		public DateTime? CompletedAt { get; set; }
		public string ApprovalComment { get; set; }
		public string DeclineReason { get; set; }
		public string ApprovalIPAddress { get; set; }
		public string ApprovalUserAgent { get; set; }
		public bool RequireEmailOtp { get; set; }
		public bool EmailOtpVerified { get; set; }
		public DateTime? EmailOtpSentAt { get; set; }
		public DateTime? EmailOtpExpiresAt { get; set; }
		public DateTime? EmailOtpVerifiedAt { get; set; }
		public int EmailOtpAttempts { get; set; }
		public string EmailOtpCodeHash { get; set; }
		public SignerAuthenticationMethod AuthenticationMethod { get; set; }
		public SignatureModel SignatureInformation { get; set; }
		public string SignatureImage { get; set; }
		public SignerStatus SignerStatus {
			get { return m_signerStatus; }
			set {

				if (value > this.SignerStatus) { 

					m_signerStatus = value;

					StatusChanged.Add(new StatusChanged() {
						SignerStatus = value,
						TimeStamp = DateTime.Now
					});
				}
			}

		}
		public List<StatusChanged> StatusChanged { get; set; } = new List<StatusChanged>();

		public void RecordStatusEvent(SignerStatus status, DateTime? timestamp = null) {
			if (!StatusChanged.Any(item => item.SignerStatus == status)) {
				StatusChanged.Add(new StatusChanged() {
					SignerStatus = status,
					TimeStamp = timestamp ?? DateTime.Now
				});
			}

			if (status > m_signerStatus) {
				m_signerStatus = status;
			}
		}
	}

	public class StatusChanged {
		public SignerStatus SignerStatus { get; set; }
		public DateTime TimeStamp { get; set; }
	}

	public enum SignerStatus {
		None,
		Sent,
		Received,
		Opened,
		Signed
	}

	public enum SignerAuthenticationMethod {
		EmailLink,
		EmailOtp,
		SignerAccount
	}

	public enum EnvelopeWorkflowMode {
		Simple,
		Complex
	}

	public enum RecipientRole {
		Signer,
		Approver,
		Cc,
		Observer
	}

	public enum EnvelopeStatus {
		Incomplete,
		New,
		Sent,
		Signed,
		Faulted,
		Closed
	}
}
