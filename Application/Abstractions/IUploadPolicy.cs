using System.IO;

namespace SignFabric.Application.Abstractions {
	public interface IUploadPolicy {
		string AcceptAttribute { get; }
		bool IsAllowed(string fileName, long length, out string errorMessage);
	}
}
