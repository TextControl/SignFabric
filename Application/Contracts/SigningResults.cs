using SignFabric.Domain;

namespace SignFabric.Application.Contracts {
	public class ValidatedDocument {
		public Envelope Envelope { get; set; }
		public bool Valid { get; set; }
		public string ErrorMessage { get; set; }
	}

	public class SignedDocumentModel {
		public SignatureModel SignatureModel { get; set; }
		public Envelope Envelope { get; set; }
		public string SignerId { get; set; }
		public string SignatureImage { get; set; }
	}
}
