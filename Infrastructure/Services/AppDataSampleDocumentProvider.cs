using SignFabric.Application.Abstractions;
using SignFabric.Infrastructure.Configuration;
using System.IO;

namespace SignFabric.Infrastructure.Services {
	public class AppDataSampleDocumentProvider : ISampleDocumentProvider {
		private readonly AppSettingsPathResolver _paths;

		public AppDataSampleDocumentProvider(AppSettingsPathResolver paths) {
			_paths = paths;
		}

		public MemoryStream OpenSample(string fileName) {
			return new MemoryStream(File.ReadAllBytes(Path.Combine(_paths.DataDirectory, fileName)));
		}
	}
}
