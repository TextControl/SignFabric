using SignFabric.Application.Abstractions;
using SignFabric.Application.Services;
using SignFabric.Domain;
using SignFabric.Infrastructure.Configuration;
using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Storage {
	public class SignerDocumentService : ISignerDocumentService {
		private readonly AppSettingsPathResolver _paths;

		public SignerDocumentService(AppSettingsPathResolver paths) {
			_paths = paths ?? throw new ArgumentNullException(nameof(paths));
		}

		public Task<List<Envelope>> GetSignedDocumentsAsync(string signerEmail) =>
			Task.Run(() => FindSignedEnvelopes(signerEmail)
				.OrderByDescending(envelope => envelope.Sent)
				.ToList());

		public Task<EnvelopeDetailsView> GetSignedDocumentDetailsAsync(string signerEmail, string envelopeId) {
			return Task.Run(() => {
				var match = FindSignedEnvelope(signerEmail, envelopeId);

				using var db = OpenEnvelopeDatabase(match.DatabasePath);
				var signatureImages = new Dictionary<string, string>();

				foreach (var signer in match.Envelope.Signers.Where(signer => signer.SignerStatus == SignerStatus.Signed && signer.SignatureInformation != null)) {
					var signatureImage = DownloadBase64(db, $"$/envelopes/{match.Envelope.EnvelopeID}/signatures/{signer.Id}");
					if (!string.IsNullOrWhiteSpace(signatureImage)) {
						signatureImages[signer.Id] = signatureImage;
					}
				}

				return new EnvelopeDetailsView {
					Envelope = match.Envelope,
					ThumbnailSvg = DownloadBase64(db, $"$/envelopes/{match.Envelope.EnvelopeID}/thumbnail"),
					SignatureImages = signatureImages
				};
			});
		}

		public Task<(byte[] Document, string FileName)> DownloadSignedDocumentAsync(string signerEmail, string envelopeId) {
			return Task.Run(() => {
				var match = FindSignedEnvelope(signerEmail, envelopeId);
				using var db = OpenEnvelopeDatabase(match.DatabasePath);
				var document = DownloadBytes(db, $"$/envelopes/{match.Envelope.EnvelopeID}/final");

				if (document == null || document.Length == 0) {
					throw new InvalidOperationException("The final signed document could not be loaded.");
				}

				return (document, match.Envelope.Name);
			});
		}

		private SignedEnvelopeMatch FindSignedEnvelope(string signerEmail, string envelopeId) =>
			FindSignedEnvelopeMatches(signerEmail)
				.FirstOrDefault(match => string.Equals(match.Envelope.EnvelopeID, envelopeId, StringComparison.OrdinalIgnoreCase))
				?? throw new InvalidOperationException("Signed document not found.");

		private IEnumerable<Envelope> FindSignedEnvelopes(string signerEmail) =>
			FindSignedEnvelopeMatches(signerEmail).Select(match => match.Envelope);

		private IEnumerable<SignedEnvelopeMatch> FindSignedEnvelopeMatches(string signerEmail) {
			if (string.IsNullOrWhiteSpace(signerEmail)) {
				yield break;
			}

			var userDirectory = Path.Combine(_paths.DatabaseDirectory, "users");
			if (!Directory.Exists(userDirectory)) {
				yield break;
			}

			foreach (var databasePath in Directory.EnumerateFiles(userDirectory, "envelopes.db", SearchOption.AllDirectories)) {
				List<Envelope> envelopes;
				try {
					using var db = OpenEnvelopeDatabase(databasePath);
					envelopes = db.GetCollection<Envelope>("envelope").Query().ToList();
				}
				catch {
					continue;
				}

				foreach (var envelope in envelopes.Where(envelope => IsAccessibleSignedEnvelope(envelope, signerEmail))) {
					yield return new SignedEnvelopeMatch(envelope, databasePath);
				}
			}
		}

		private static bool IsAccessibleSignedEnvelope(Envelope envelope, string signerEmail) =>
			envelope?.Status == EnvelopeStatus.Signed &&
			envelope.Signers.Any(signer =>
				signer.SignerStatus == SignerStatus.Signed &&
				string.Equals(signer.Email, signerEmail, StringComparison.OrdinalIgnoreCase));

		private static LiteDatabase OpenEnvelopeDatabase(string databasePath) =>
			new($"Filename={databasePath}; Connection=shared");

		private static string DownloadBase64(LiteDatabase db, string fileId) {
			var bytes = DownloadBytes(db, fileId);
			return bytes == null ? null : Convert.ToBase64String(bytes);
		}

		private static byte[] DownloadBytes(LiteDatabase db, string fileId) {
			var fs = db.FileStorage;
			if (!fs.Exists(fileId)) {
				return null;
			}

			using var stream = new MemoryStream();
			fs.Download(fileId, stream);
			return stream.ToArray();
		}

		private record SignedEnvelopeMatch(Envelope Envelope, string DatabasePath);
	}
}
