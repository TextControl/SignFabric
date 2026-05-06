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
		public List<Signer> Signers { get; set; } = new List<Signer>();
		public EnvelopeStatus Status { get; set; }
		public string FaultMessage { get; set; }
		public bool ContainsSignatureBoxes { get; set; }
		public SignatureModel SignatureInformation { get; set; }
	}

	public class Signer {
		private SignerStatus m_signerStatus;

		public string Id { get; set; }
		public string Name { get; set; }
		public string Email { get; set; }
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

	public enum EnvelopeStatus {
		Incomplete,
		New,
		Sent,
		Signed,
		Faulted,
		Closed
	}
}
