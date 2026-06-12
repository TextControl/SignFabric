using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using SignFabric.Infrastructure.Storage.LiteDb;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Services.TextControl {
	public static class EnvelopeDocument {
		public static string ProcessNewDocument(MemoryStream ms, string Filename, EnvelopeStore Store, string UserName, string UserId, string SigningCertificateId) {

			ms.Position = 0;
			string image;
			byte[] data = ms.ToArray();
			bool bContainsSignatureBoxes;

			// create thumbnail and check for signature boxes
			using (TextControlHelpers tx = new TextControlHelpers(Convert.ToBase64String(data))) {
				image = tx.GetThumbnail();
				bContainsSignatureBoxes = false;
				ms = tx.GetInternalFormat();
			}

			if (ms == null || ms.Length == 0) {
				return null;
			}

			// new Envelope object to be stored
			Envelope envelope = new Envelope() {
				Created = DateTime.Now,
				Status = EnvelopeStatus.Incomplete,
				Sender = UserName,
				UserID = UserId,
				Name = Filename,
				EnvelopeID = Guid.NewGuid().ToString(),
				SigningCertificateId = SigningCertificateId,
				ContainsSignatureBoxes = bContainsSignatureBoxes
			};

			Store.Add(envelope, ms);

		    Store.AddThumbnail(envelope, image);

			return envelope.EnvelopeID;
		}
	}
}
