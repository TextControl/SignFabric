using System.IO;

namespace SignFabric.Application.Abstractions {
	public interface ISampleDocumentProvider {
		MemoryStream OpenSample(string fileName);
	}
}
