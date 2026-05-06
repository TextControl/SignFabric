using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Infrastructure.Configuration;
using SignFabric.Presentation.ViewModels;
using LiteDB;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Storage.LiteDb {
	public class EnvelopeStore {

		private readonly string _userId;
		private readonly AppSettingsPathResolver _paths;

		public EnvelopeStore(string userId, AppSettingsPathResolver paths) {
			_userId = userId;
			_paths = paths ?? throw new ArgumentNullException(nameof(paths));
		}

		private string ConnectionString => $"Filename={_paths.GetUserDatabasePath("envelopes", _userId)}; Connection=shared";

		public static string CalculateMD5(byte[] document) {
			using (var md5 = MD5.Create()) {
				return BitConverter.ToString(md5.ComputeHash(document)).Replace("-", "").ToLower();
			}
		}

		public void Add(Envelope envelope, MemoryStream stream) {
			using (var db = new LiteDatabase(ConnectionString)) {
				var col = db.GetCollection<Envelope>("envelope");

				// upload document to db
				var fs = db.FileStorage;
				stream.Position = 0;
				fs.Upload("$/envelopes/" + envelope.EnvelopeID + "/original", envelope.EnvelopeID, stream);

				col.Insert(envelope);
			}
		}

		public void UploadSignedDocument(Envelope envelope, MemoryStream stream, string signerId) {
			using (var db = new LiteDatabase(ConnectionString)) {
				var col = db.GetCollection<Envelope>("envelope");

				// upload document to db
				var fs = db.FileStorage;
				stream.Position = 0;
				fs.Upload("$/envelopes/" + envelope.EnvelopeID + "/signed/" + signerId, envelope.EnvelopeID, stream);
			}
		}

		public void UploadSignatureImage(Envelope envelope, MemoryStream stream, string signerId) {
			using (var db = new LiteDatabase(ConnectionString)) {
				var col = db.GetCollection<Envelope>("envelope");

				// upload document to db
				var fs = db.FileStorage;
				stream.Position = 0;
				fs.Upload("$/envelopes/" + envelope.EnvelopeID + "/signatures/" + signerId, envelope.EnvelopeID, stream);
			}
		}

		public string GetSignatureImage(string envelopeId, string signerId) {
			using (var db = new LiteDatabase(ConnectionString)) {
				var fs = db.FileStorage;

				using (MemoryStream ms = new MemoryStream()) {
					fs.Download("$/envelopes/" + envelopeId + "/signatures/" + signerId, ms);
					var fileBytes = ms.ToArray();
					return Convert.ToBase64String(fileBytes);
				}
			}
		}

		public string GetSignatureImageRaw(string envelopeId, string signerId) {
			using (var db = new LiteDatabase(ConnectionString)) {
				var fs = db.FileStorage;

				using (MemoryStream ms = new MemoryStream()) {
					fs.Download("$/envelopes/" + envelopeId + "/signatures/" + signerId, ms);

					var test = Encoding.UTF8.GetString(ms.ToArray()).TrimEnd((Char)0).Trim(new char[] { '\uFEFF', '\u200B' });
					var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(test);


					return System.Convert.ToBase64String(plainTextBytes);
				}
			}
		}

		public string GetSignedDocument(string envelopeId, string signerId) {
			using (var db = new LiteDatabase(ConnectionString)) {
				var fs = db.FileStorage;

				using (MemoryStream ms = new MemoryStream()) {
					fs.Download("$/envelopes/" + envelopeId + "/signed/" + signerId, ms);
					var fileBytes = ms.ToArray();
					return Convert.ToBase64String(fileBytes);
				}
			}
		}

		public void UploadFinalSignedDocument(Envelope envelope, MemoryStream stream) {
			using (var db = new LiteDatabase(ConnectionString)) {
				var col = db.GetCollection<Envelope>("envelope");

				// upload document to db
				var fs = db.FileStorage;
				stream.Position = 0;
				fs.Upload("$/envelopes/" + envelope.EnvelopeID + "/final", envelope.EnvelopeID, stream);
			}
		}

		public void UpdateFile(Envelope envelope, MemoryStream stream) {
			using (var db = new LiteDatabase(ConnectionString)) {
				var col = db.GetCollection<Envelope>("envelope");

				// upload document to db
				var fs = db.FileStorage;
				stream.Position = 0;
				fs.Upload("$/envelopes/" + envelope.EnvelopeID + "/original", envelope.EnvelopeID, stream);
			}
		}

		public void Update(string envelopeId, Envelope envelope) {
			using (var db = new LiteDatabase(ConnectionString)) {
				var col = db.GetCollection<Envelope>("envelope");

				col.Update(envelope);
			}
		}

		public void AddThumbnail(Envelope envelope, string svgContent) {
			using (var db = new LiteDatabase(ConnectionString)) {
				var col = db.GetCollection<Envelope>("envelope");

				// upload document to db
				var fs = db.FileStorage;
				using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svgContent))) {
					fs.Upload("$/envelopes/" + envelope.EnvelopeID + "/thumbnail", envelope.EnvelopeID, stream);
				}
			}
		}

		public void Delete(Envelope envelope) {
			using (var db = new LiteDatabase(ConnectionString)) {
				var col = db.GetCollection<Envelope>("envelope");
				col.Insert(envelope);
			}
		}

		public string GetDocument(string envelopeId) {
			using (var db = new LiteDatabase(ConnectionString)) {
				var fs = db.FileStorage;

				using (MemoryStream ms = new MemoryStream()) { 
					fs.Download("$/envelopes/" + envelopeId + "/original", ms);
					var fileBytes = ms.ToArray();

					return Convert.ToBase64String(fileBytes);
				}
			}
		}

		public string GetFinalSignedDocument(string envelopeId) {
			using (var db = new LiteDatabase(ConnectionString)) {
				var fs = db.FileStorage;

				using (MemoryStream ms = new MemoryStream()) {
					fs.Download("$/envelopes/" + envelopeId + "/final", ms);
					var fileBytes = ms.ToArray();
					return Convert.ToBase64String(fileBytes);
				}
			}
		}

		public string GetThumbnail(string envelopeId) {
			using (var db = new LiteDatabase(ConnectionString)) {
				var fs = db.FileStorage;

				using (MemoryStream ms = new MemoryStream()) {
					if (fs.Exists("$/envelopes/" + envelopeId + "/thumbnail") == true) {
						fs.Download("$/envelopes/" + envelopeId + "/thumbnail", ms);
						var fileBytes = ms.ToArray();
						return Convert.ToBase64String(fileBytes);
					}
					else
						return null;
				}
			}
		}

		public List<Envelope> GetEnvelopes(string envelopeId = null) {
			
			using (var db = new LiteDatabase(ConnectionString)) {
				var col = db.GetCollection<Envelope>("envelope");
				List<Envelope> results = null;

				if (envelopeId == null) {
					results = col.Query().ToList();
				}
				else {
					results = col.Query()
						.Where(x => x.EnvelopeID == envelopeId)
						.ToList();
				}

				return results;
			}
		}

	}
}
