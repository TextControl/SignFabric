using SignFabric.Application.Abstractions;
using SignFabric.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;

namespace SignFabric.Infrastructure.Services {
	public class ConfiguredUploadPolicy : IUploadPolicy {
		private readonly AppSettings _settings;

		public ConfiguredUploadPolicy(IOptions<AppSettings> settings) {
			_settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
		}

		public string AcceptAttribute => string.Join(", ", _settings.AllowedFileTypes
			.Where(extension => !string.IsNullOrWhiteSpace(extension))
			.Select(NormalizeExtension)
			.Distinct(StringComparer.OrdinalIgnoreCase));

		public bool IsAllowed(string fileName, long length, out string errorMessage) {
			if (_settings.MaxFileSize > 0 && length > _settings.MaxFileSize) {
				errorMessage = $"The selected file exceeds the maximum upload size of {_settings.MaxFileSize} bytes.";
				return false;
			}

			var extension = Path.GetExtension(fileName);
			if (_settings.AllowedFileTypes.Any() &&
				!_settings.AllowedFileTypes.Any(allowed => string.Equals(NormalizeExtension(allowed), extension, StringComparison.OrdinalIgnoreCase))) {
				errorMessage = $"The selected file type '{extension}' is not allowed.";
				return false;
			}

			errorMessage = null;
			return true;
		}

		private static string NormalizeExtension(string extension) {
			var normalized = extension?.Trim() ?? string.Empty;
			return normalized.StartsWith(".")
				? normalized
				: "." + normalized;
		}
	}
}
