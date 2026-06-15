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
			try {
				tx.Load(ls, Convert.FromBase64String(document));
			} catch (Exception ex) {
				tx.Dispose();
				throw new InvalidOperationException("The selected file could not be opened as a supported document. Please upload a valid PDF, DOCX, RTF, DOC, HTML, or TX document.", ex);
			}
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
			try {
				tx.Save(out data, BinaryStreamType.InternalUnicodeFormat);
			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine($"Error converting document to internal format: {ex.Message}");
				return null;
			}

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
				mm.RemoveEmptyFields = false;

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

		public List<FieldAssignmentField> GetUnassignedRecipientFields(List<Signer> signers) {
			var fields = new List<FieldAssignmentField>();
			var signerIds = new HashSet<string>((signers ?? new List<Signer>()).Select(signer => signer.Id));
			var seen = new HashSet<string>();
			int formIndex = 1;

			foreach (FormField field in tx.FormFields) {
				string fieldName = field.Name ?? string.Empty;
				string signerId = GetFieldSignerId(fieldName);

				if (!string.IsNullOrWhiteSpace(signerId) && signerIds.Contains(signerId)) {
					continue;
				}

				string fieldId = "form:" + fieldName;
				if (!seen.Add(fieldId)) {
					continue;
				}

				fields.Add(new FieldAssignmentField {
					FieldId = fieldId,
					Name = fieldName,
					Label = GetFormFieldLabel(field, formIndex),
					FieldType = "Form Field"
				});

				formIndex++;
			}

			foreach (ApplicationField field in tx.ApplicationFields) {
				if (field.TypeName != "MERGEFIELD" || field.Parameters == null || field.Parameters.Length == 0) {
					continue;
				}

				string fieldName = field.Parameters[0];
				if (fieldName != "signer_name" && fieldName != "signer_email") {
					continue;
				}

				string fieldId = "merge:" + fieldName;
				if (!seen.Add(fieldId)) {
					continue;
				}

				fields.Add(new FieldAssignmentField {
					FieldId = fieldId,
					Name = fieldName,
					Label = fieldName == "signer_name" ? "Signer Name" : "Signer Email",
					FieldType = "Auto-fill Data"
				});
			}

			int signatureIndex = 1;
			foreach (SignatureField signatureField in tx.SignatureFields) {
				string signatureName = signatureField.Name ?? string.Empty;
				string signerId = GetSignatureSignerId(signatureName);
				string fieldId = "signature:" + signatureIndex;
				signatureIndex++;

				if (!string.IsNullOrWhiteSpace(signerId) && signerIds.Contains(signerId)) {
					continue;
				}

				fields.Add(new FieldAssignmentField {
					FieldId = fieldId,
					Name = signatureName,
					Label = "Signature Box " + (signatureIndex - 1),
					FieldType = "Signature Field"
				});
			}

			return fields;
		}

		public byte[] AssignRecipientFields(List<FieldAssignmentMapping> assignments) {
			var assignmentMap = (assignments ?? new List<FieldAssignmentMapping>())
				.Where(assignment => !string.IsNullOrWhiteSpace(assignment.FieldId) && !string.IsNullOrWhiteSpace(assignment.SignerId))
				.GroupBy(assignment => assignment.FieldId)
				.ToDictionary(group => group.Key, group => group.First().SignerId);

			foreach (FormField field in tx.FormFields) {
				string fieldName = field.Name ?? string.Empty;
				if (!assignmentMap.TryGetValue("form:" + fieldName, out string signerId)) {
					continue;
				}

				field.Name = signerId + ":" + GetFieldNameSuffix(fieldName);
			}

			foreach (ApplicationField field in tx.ApplicationFields) {
				if (field.TypeName != "MERGEFIELD" || field.Parameters == null || field.Parameters.Length == 0) {
					continue;
				}

				string fieldName = field.Parameters[0];
				if (!assignmentMap.TryGetValue("merge:" + fieldName, out string signerId)) {
					continue;
				}

				var parameters = field.Parameters;
				parameters[0] = "signer_" + SanitizeMergeFieldName(signerId) + (fieldName == "signer_email" ? "_email" : "_name");
				field.Parameters = parameters;
			}

			int signatureIndex = 1;
			foreach (SignatureField signatureField in tx.SignatureFields) {
				if (assignmentMap.TryGetValue("signature:" + signatureIndex, out string signerId)) {
					signatureField.Name = "txsign_" + signerId;
				}

				signatureIndex++;
			}

			byte[] data;
			tx.Save(out data, BinaryStreamType.InternalUnicodeFormat);
			return data;
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
							try {
								var signatureImage = Convert.FromBase64String(_store.GetSignatureImage(envelope.EnvelopeID, signer.Id));
								var memStream = new MemoryStream(signatureImage, 0, signatureImage.Length, writable: false, publiclyVisible: true);
								signatureField.Image = new SignatureImage(memStream);
							}
							catch {
								// Some valid electronic signature flows do not return a separate rendered signature image.
							}

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
				tx.Save(out data, TXTextControl.BinaryStreamType.AdobePDF, saveSettings);
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

		private static string GetFieldSignerId(string fieldName) {
			if (string.IsNullOrWhiteSpace(fieldName) || !fieldName.Contains(':')) {
				return null;
			}

			var signerId = fieldName.Split(':')[0];
			return signerId == "undefined" || signerId == "unassigned" ? null : signerId;
		}

		private static string GetSignatureSignerId(string signatureName) {
			if (string.IsNullOrWhiteSpace(signatureName) || !signatureName.StartsWith("txsign_")) {
				return null;
			}

			var signerId = signatureName.Substring("txsign_".Length);
			return signerId == "undefined" || signerId.StartsWith("unassigned") ? null : signerId;
		}

		private static string GetFieldNameSuffix(string fieldName) {
			if (string.IsNullOrWhiteSpace(fieldName)) {
				return Guid.NewGuid().ToString();
			}

			if (!fieldName.Contains(':')) {
				return fieldName;
			}

			var suffix = fieldName.Substring(fieldName.IndexOf(':') + 1);
			return string.IsNullOrWhiteSpace(suffix) ? Guid.NewGuid().ToString() : suffix;
		}

		private static string GetFormFieldLabel(FormField field, int index) {
			if (field is CheckFormField) {
				return "Checkbox " + index;
			}

			if (field is SelectionFormField) {
				return "Drop-Down " + index;
			}

			if (field is DateFormField) {
				return "Date Picker " + index;
			}

			return "Text Form Field " + index;
		}

		private static string SanitizeMergeFieldName(string value) {
			var builder = new StringBuilder();

			foreach (char character in value ?? string.Empty) {
				builder.Append(char.IsLetterOrDigit(character) ? character : '_');
			}

			return builder.ToString().Trim('_');
		}

		public bool ContainsSignatureBox(List<Signer> signers) {
			if (signers == null || signers.Count == 0) {
				return true;
			}

			var requiredSignatureNames = new HashSet<string>(
				signers.Select(signer => "txsign_" + signer.Id),
				StringComparer.OrdinalIgnoreCase);

			foreach (TXTextControl.SignatureField signatureField in tx.SignatureFields) {
				if (!string.IsNullOrWhiteSpace(signatureField.Name)) {
					requiredSignatureNames.Remove(signatureField.Name);
				}
			}

			return requiredSignatureNames.Count == 0;
		}

	}


}
