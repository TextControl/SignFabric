using System.IO;

namespace SignFabric.Application.Abstractions {
	public interface IEnvelopeDocumentFactory {
		string CreateEnvelopeFromDocument(string userId, string userName, MemoryStream documentStream, string fileName);
	}
}
