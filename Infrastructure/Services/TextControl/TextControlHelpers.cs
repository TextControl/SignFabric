using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Infrastructure.Configuration;
using SignFabric.Infrastructure.Storage.LiteDb;
using SignFabric.Presentation.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using TXTextControl;
using TXTextControl.ServerVisualisation;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SignFabric.Infrastructure.Services.TextControl {
	public class TextControlHelpers : IDisposable {

        LoadSettings ls = new LoadSettings() { ApplicationFieldFormat = ApplicationFieldFormat.MSWordTXFormFields };
        TextViewGenerator tx;

		public TextControlHelpers(string document) {
			tx = new TextViewGenerator();
			tx.Create();
			tx.Load(ls, Convert.FromBase64String(document));
		}

		public TextControlHelpers() {
			tx = new TextViewGenerator();
			tx.Create();
		}

		public void Dispose() {
			tx.Dispose();
		}

		public MemoryStream GetInternalFormat() {
			byte[] data = null;
			tx.Save(out data, BinaryStreamType.InternalUnicodeFormat);

			return new MemoryStream(data);
		}

		public bool HasTrackedChanges() {
			var hasChanges = false;

			if (tx.TrackedChanges.Count > 0)
				hasChanges = true;

			return hasChanges;
		}

		public MemoryStream MergeJson(string Json) {
			using (TXTextControl.DocumentServer.MailMerge mm = new TXTextControl.DocumentServer.MailMerge()) {
				mm.TextComponent = tx;

				mm.MergeJsonData(Json);

				byte[] data = null;
				tx.Save(out data, BinaryStreamType.InternalUnicodeFormat);

				return new MemoryStream(data);
			}
		}

		public string GetThumbnail() {
			return tx.GetPages()[1].GetImage(Page.PageContent.All, 300);
		}

		public List<FieldModel> GetMergeFields() {

			List<FieldModel> appFields = new List<FieldModel>();

			foreach(ApplicationField field in tx.ApplicationFields) {

				if (field.TypeName != "MERGEFIELD")
					continue;
				
				appFields.Add(new FieldModel() { 
					Name = field.Parameters[0],
					Value = field.Text
				});
			}

			return appFields;
		}

		public List<SectionModel> GetSubTextParts() {

			List<SectionModel> sections = new List<SectionModel>();

			foreach (SubTextPart textPart in tx.SubTextParts) {

				sections.Add(new SectionModel() {
					Name = textPart.Name,
					Active = true
				});
			}

			return sections;
		}

		public byte[] ConditionToId(bool reverse = false) {
			((TextViewGenerator)tx).IsFormFieldValidationEnabled = true;
			
			if (reverse == true)
				tx.FormFields.ConditionalInstructions.Clear();

			foreach (FormField field in tx.FormFields) { 

				if (reverse == false) { 
					foreach (ConditionalInstruction instruction in ((TextViewGenerator)tx).FormFields.ConditionalInstructions.GetItems(field)) {
						if (instruction.Conditions[0].ComparisonOperator == Condition.ComparisonOperators.Is &&
							instruction.Conditions[0].ComparisonValue == null &&
							instruction.Conditions[0].ComparisonValueType == Condition.ComparisonValueTypes.NoValue) {

							if (instruction.Instructions[0].InstructionType == Instruction.InstructionTypes.IsValueValid &&
								(bool)instruction.Instructions[0].InstructionValue == false) {
								field.ID = 1;
							}
						}
						else
							field.ID = 0;
					}
				}
				else {

					if (field.ID == 1) {

						if (field is TextFormField) {
							ConditionalInstruction instruction = new ConditionalInstruction(Guid.NewGuid().ToString(),
								new Condition[] { new Condition((TextFormField)field, Condition.ComparisonOperators.Is, null) },
								new Instruction[] { new Instruction((TextFormField)field, Instruction.InstructionTypes.IsValueValid, false, true) });

							tx.FormFields.ConditionalInstructions.Add(instruction);
						}

						if (field is DateFormField) {
							ConditionalInstruction instruction = new ConditionalInstruction(Guid.NewGuid().ToString(),
								new Condition[] { new Condition((DateFormField)field, Condition.ComparisonOperators.Is, null) },
								new Instruction[] { new Instruction((DateFormField)field, Instruction.InstructionTypes.IsValueValid, false) });

							tx.FormFields.ConditionalInstructions.Add(instruction);
						}

					}
				}
			}

			byte[] data;
			tx.Save(out data, BinaryStreamType.InternalUnicodeFormat);
			return data;
		}

		public string GetDocumentAccessId(byte[] document) {
			TXTextControl.LoadSettings ls = new LoadSettings();

			try {
				tx.Load(document, BinaryStreamType.AdobePDF, ls);
			}
			catch { return null; }

			if (ls.EmbeddedFiles == null)
				return null;

			foreach (EmbeddedFile file in ls.EmbeddedFiles) {
				if (file.FileName == "__txesign_documentaccessid.txt")
					return System.Text.Encoding.Unicode.GetString((byte[])file.Data);
            }

			return null;
		}

		public byte[] PrepareFormFields(Signer signer) {

			DeleteFormFields(tx, signer.Id);

			byte[] data;
			tx.Save(out data, BinaryStreamType.InternalUnicodeFormat);
			return data;
		}

		public (byte[] Data, string Thumbnail) CreatePDF(Envelope envelope, string userId, AppSettingsPathResolver paths, X509Certificate2 cert) {

			var _store = new EnvelopeStore(userId, paths);

			TXTextControl.SaveSettings saveSettings = new TXTextControl.SaveSettings();

			List<DigitalSignature> signatures = new List<DigitalSignature>();

			using (TXTextControl.ServerTextControl svr = new ServerTextControl()) {

				svr.Create();

				int i = 0;

				foreach (Signer signer in envelope.Signers) {
						
					svr.Load(Convert.FromBase64String(_store.GetSignedDocument(envelope.EnvelopeID, signer.Id)), BinaryStreamType.InternalUnicodeFormat);

					foreach (FormField sourceFormField in svr.FormFields) {
						foreach (FormField destinationFormField in tx.FormFields) {
							if (sourceFormField.Name == destinationFormField.Name)
								destinationFormField.Text = sourceFormField.Text;
						}
					}

					foreach (TXTextControl.SignatureField signatureField in tx.SignatureFields) {
						if (signatureField.Name == "txsign_" + signer.Id) {
							var signatureImage = Convert.FromBase64String(_store.GetSignatureImage(envelope.EnvelopeID, signer.Id));
							var memStream = new MemoryStream(signatureImage, 0, signatureImage.Length, writable: false, publiclyVisible: true);
							signatureField.Image = new SignatureImage(memStream);

							signatures.Add(new DigitalSignature(cert, null, signatureField.Name));
						}
					}

					i++;
				}

			}

			byte[] octets = System.Text.Encoding.ASCII.GetBytes(envelope.EnvelopeID + ":" + userId);
			var envelope_code = Convert.ToBase64String(octets);

			// add the signatures
			saveSettings.SignatureFields = signatures.ToArray();

            saveSettings.CreatorApplication = "Text Control SignFabric";

            saveSettings.EmbeddedFiles = new EmbeddedFile[] {
                new EmbeddedFile("__txesign_documentaccessid.txt", envelope_code, null) {
                        Relationship = "Data",
                        MIMEType = "text/plain"
                }
            };

            byte[] data;

			DeleteFormFields(tx);

			try {
				tx.Save(out data, TXTextControl.BinaryStreamType.AdobePDFA, saveSettings);
			} catch (Exception ex) {
				throw new InvalidOperationException(
					"The final signed PDF could not be created. Please check the signing certificate and signature fields, then try again.",
					ex);
			}

			string thumbnail = null;
			try {
				thumbnail = GetThumbnail();
			} catch {
				thumbnail = null;
			}

			return (data, thumbnail);
		}

		private void DeleteFormFields(ServerTextControl tx, string signerId = null) {

			if (tx.FormFields.Count == 0)
				return;

			bool bRemovedField = false;

			foreach (FormField field in tx.FormFields) {

				if (signerId != null) {
					var fieldSignerId = field.Name.Split(":")[0];

					if (fieldSignerId == signerId)
						continue;
				}

				tx.Selection.Start = field.Start;

				var text = field.Text;

				tx.FormFields.Remove(field);
				tx.Selection.Text = text;

				bRemovedField = true;

				break;
			}

			if(bRemovedField == true)
				DeleteFormFields(tx, signerId);
		}

		public bool ContainsSignatureBox(List<Signer> signers) {

			int count = 0;

			foreach (Signer signer in signers) { 

				foreach (IFormattedText textPart in tx.TextParts) {
					
					foreach (FrameBase frame in textPart.Frames) {
						if (frame is TXTextControl.SignatureField && frame.Name == "txsign_" + signer.Id) {
							count++; continue;
						}
					}

				}

			}

			return (count == signers.Count) ? true : false;
		}

	}


}
