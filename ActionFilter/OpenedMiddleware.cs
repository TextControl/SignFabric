using SignFabric.Application.Abstractions;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.ActionFilter {
	public class OpenedMiddleware {
		private readonly RequestDelegate m_next;
		private readonly ILogger<OpenedMiddleware> _logger;

		public OpenedMiddleware(RequestDelegate next, ILogger<OpenedMiddleware> logger) {
			m_next = next;
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		public async Task Invoke(HttpContext context, IStoreRepositoryFactory storeFactory) {
			if (context.Request.Query.TryGetValue("opened", out var openedValues) &&
				!string.IsNullOrWhiteSpace(openedValues.FirstOrDefault())) {
				TrackOpenedEnvelope(openedValues.FirstOrDefault(), storeFactory);
			}

			if (m_next != null) {
				await m_next.Invoke(context);
			}
		}

		private void TrackOpenedEnvelope(string openedId, IStoreRepositoryFactory storeFactory) {
			try {
				byte[] octets = Convert.FromBase64String(openedId);
				var structureFolder = System.Text.Encoding.ASCII.GetString(octets).Split(':');

				if (structureFolder.Length < 3 ||
					string.IsNullOrWhiteSpace(structureFolder[0]) ||
					string.IsNullOrWhiteSpace(structureFolder[1]) ||
					string.IsNullOrWhiteSpace(structureFolder[2])) {
					_logger.LogWarning("Ignoring malformed opened tracking value.");
					return;
				}

				var store = storeFactory.CreateEnvelopeRepository(structureFolder[1]);
				var envelope = store.GetEnvelopes(structureFolder[0]).FirstOrDefault();

				if (envelope == null) {
					_logger.LogWarning("Ignoring opened tracking value for missing envelope {EnvelopeId}.", structureFolder[0]);
					return;
				}

				var signer = envelope.Signers.FirstOrDefault(signer => signer.Id == structureFolder[2]);
				if (signer == null) {
					_logger.LogWarning("Ignoring opened tracking value for missing signer {SignerId}.", structureFolder[2]);
					return;
				}

				if (signer.SignerStatus == SignerStatus.None || signer.SignerStatus == SignerStatus.Sent) {
					signer.SignerStatus = SignerStatus.Received;
					store.Update(envelope.EnvelopeID, envelope);
				}
			} catch (FormatException ex) {
				_logger.LogWarning(ex, "Ignoring invalid opened tracking value.");
			} catch (Exception ex) {
				_logger.LogError(ex, "Failed to process opened tracking value.");
			}
		}
	}
}
