using SignFabric.Infrastructure.Configuration;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SignFabric.Infrastructure.Email.Legacy {
	public class ConfirmationEmail {
		private const string TemplateMetadataFileName = "email-template-settings.json";

		private readonly Credentials _credentials;
		private readonly AppSettingsPathResolver _paths;

		public ConfirmationEmail(Credentials credentials, AppSettingsPathResolver paths) {
			_credentials = credentials;
			_paths = paths;
		}

		private string ReadTemplate(string fileName) =>
			System.IO.File.ReadAllText(Path.Combine(_paths.EmailTemplatesPath, fileName));

		private string RenderTemplate(string templateFileName, IDictionary<string, string> values, string title, string preheader) {
			var body = ApplyPlaceholders(ReadTemplate(templateFileName), values);
			var layoutValues = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase) {
				["%%%title%%%"] = title,
				["%%%preheader%%%"] = preheader,
				["%%%body%%%"] = body
			};

			return ApplyPlaceholders(ReadTemplate("email-layout.html"), layoutValues);
		}

		private string RenderTemplate(string templateFileName, IDictionary<string, string> values, out string subject) {
			var settings = GetTemplateMetadata(templateFileName);
			subject = ApplyPlaceholders(settings.Subject, values);
			return RenderTemplate(templateFileName, values, subject, ApplyPlaceholders(settings.Preheader, values));
		}

		private static string ApplyPlaceholders(string html, IDictionary<string, string> values) {
			foreach (var value in values) {
				html = html.Replace(value.Key, value.Value ?? string.Empty);
			}

			return html;
		}

		private static string HostUrl(string host) => (host ?? string.Empty).TrimEnd('/');

		private EmailTemplateMetadata GetTemplateMetadata(string templateFileName) {
			var fallback = new EmailTemplateMetadata { Subject = templateFileName, Preheader = string.Empty };
			var path = Path.Combine(_paths.EmailTemplatesPath, TemplateMetadataFileName);

			if (!File.Exists(path)) {
				return fallback;
			}

			try {
				var metadata = JsonConvert.DeserializeObject<Dictionary<string, EmailTemplateMetadata>>(File.ReadAllText(path));
				if (metadata != null &&
					metadata.TryGetValue(templateFileName, out var settings) &&
					settings != null) {
					return new EmailTemplateMetadata {
						Subject = string.IsNullOrWhiteSpace(settings.Subject) ? templateFileName : settings.Subject,
						Preheader = settings.Preheader ?? string.Empty
					};
				}
			}
			catch {
				return fallback;
			}

			return fallback;
		}

		public void SendSigningInvitationEmail(Envelope envelope, Signer signer, string host, string userId) {
			EmailService emailService = new EmailService(_credentials);

			var envelope_code = EncodeAccessId(envelope.EnvelopeID, userId, signer.Id);

			string emailBody = RenderTemplate(
				"confirmation.html",
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
					["%%%sender_name%%%"] = envelope.Sender,
					["%%%document_name%%%"] = envelope.Name,
					["%%%envelope_code%%%"] = envelope_code,
					["%%%recipient_url%%%"] = GetRecipientUrl(host, envelope_code, envelope, signer),
					["%%%email_heading%%%"] = GetRecipientHeading(signer),
					["%%%email_action_text%%%"] = GetRecipientActionText(signer),
					["%%%button_text%%%"] = GetRecipientButtonText(signer),
					["%%%url%%%"] = HostUrl(host)
				},
				out var subject);

			emailService.Send(new EmailMessage() {
				Body = emailBody,
				Destination = signer.Email,
				Subject = subject
			});

			signer.RecordStatusEvent(SignerStatus.Sent);
		}

		public void SendConfirmationEmail(Envelope envelope, string host, string userId) {
			// send e-mail
			EmailService emailService = new EmailService(_credentials);
			

			foreach (Signer signer in envelope.Signers.Where(signer => signer.RoutingActive && !HasStatusEvent(signer, SignerStatus.Sent))) {

				var envelope_code = EncodeAccessId(envelope.EnvelopeID, userId, signer.Id);

				string emailBody = RenderTemplate(
					"confirmation.html",
					new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
						["%%%sender_name%%%"] = envelope.Sender,
						["%%%document_name%%%"] = envelope.Name,
						["%%%envelope_code%%%"] = envelope_code,
						["%%%recipient_url%%%"] = GetRecipientUrl(host, envelope_code, envelope, signer),
						["%%%email_heading%%%"] = GetRecipientHeading(signer),
						["%%%email_action_text%%%"] = GetRecipientActionText(signer),
						["%%%button_text%%%"] = GetRecipientButtonText(signer),
						["%%%url%%%"] = HostUrl(host)
					},
					out var subject);

                emailService.Send(new EmailMessage() {
					Body = emailBody,
					Destination = signer.Email,
					Subject = subject
				});

				signer.RecordStatusEvent(SignerStatus.Sent);
			}
			
		}

		public void SendReviewOwnerEmail(Contract contract, string host) {
			// send e-mail
			EmailService emailService = new EmailService(_credentials);
			string emailBody = RenderTemplate(
				"reviewed.html",
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
					["%%%signer_name%%%"] = contract.Sender,
					["%%%document_name%%%"] = contract.Name,
					["%%%url%%%"] = HostUrl(host)
				},
				out var subject);

			emailService.Send(new EmailMessage() {
				Body = emailBody,
				Destination = contract.Signer.Email,
				Subject = subject
			});
		}

		public void SendReviewEmail(Contract contract, string host, string userId) {
			// send e-mail
			EmailService emailService = new EmailService(_credentials);

			var envelope_code = EncodeAccessId(contract.ContractID, userId);

			string emailBody = RenderTemplate(
				"confirmation-contract.html",
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
					["%%%sender_name%%%"] = contract.Sender,
					["%%%document_name%%%"] = contract.Name,
					["%%%envelope_code%%%"] = envelope_code,
					["%%%url%%%"] = HostUrl(host)
				},
				out var subject);

			emailService.Send(new EmailMessage() {
				Body = emailBody,
				Destination = contract.Signer.Email,
				Subject = subject
			});
		}

		public void SendFinalSignedEmail(Envelope envelope, MemoryStream stream, Signer signer, string host) {
			// send e-mail
			EmailService emailService = new EmailService(_credentials);
			string emailBody = RenderTemplate(
				"signing-thanks_completed.html",
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
					["%%%sender_name%%%"] = envelope.Sender,
					["%%%envelope_id%%%"] = envelope.EnvelopeID,
					["%%%document_name%%%"] = envelope.Name,
					["%%%url%%%"] = HostUrl(host)
				},
				out var subject);

			stream.Position = 0;

			emailService.Send(new EmailMessage() {
				Body = emailBody,
				Destination = signer.Email,
				Subject = subject,
				Attachments = new List<System.Net.Mail.Attachment>() {
					new System.Net.Mail.Attachment(stream, Path.GetFileNameWithoutExtension(envelope.Name) + ".pdf", "application/pdf")
				}
			});
		}

		public void SendFinalizationFaultEmail(Envelope envelope, string host) {
			EmailService emailService = new EmailService(_credentials);
			string emailBody = RenderTemplate(
				"signing-failed.html",
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
					["%%%sender_name%%%"] = envelope.Sender,
					["%%%envelope_id%%%"] = envelope.EnvelopeID,
					["%%%document_name%%%"] = envelope.Name,
					["%%%fault_message%%%"] = WebUtility.HtmlEncode(envelope.FaultMessage ?? "The final signed PDF could not be created."),
					["%%%details_url%%%"] = HostUrl(host) + "/envelopes/details/" + envelope.EnvelopeID,
					["%%%url%%%"] = HostUrl(host)
				},
				out var subject);

			emailService.Send(new EmailMessage() {
				Body = emailBody,
				Destination = envelope.Sender,
				Subject = subject
			});
		}

		public void SendSignedEmail(Envelope envelope, Signer signer, string host) {
			// send e-mail
			EmailService emailService = new EmailService(_credentials);
			string emailBody = RenderTemplate(
				"signing-thanks.html",
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
					["%%%sender_name%%%"] = envelope.Sender,
					["%%%envelope_id%%%"] = envelope.EnvelopeID,
					["%%%document_name%%%"] = envelope.Name,
					["%%%url%%%"] = HostUrl(host)
				},
				out var subject);

			emailService.Send(new EmailMessage() {
				Body = emailBody,
				Destination = signer.Email,
				Subject = subject
			});
		}

		public void SendConfirmationOwnerEmail(Envelope envelope, string host) {
			// send e-mail
			EmailService emailService = new EmailService(_credentials);
			string emailBody = RenderTemplate(
				"signed.html",
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
					["%%%signer_name%%%"] = envelope.Signers[0].Email,
					["%%%document_name%%%"] = envelope.Name,
					["%%%url%%%"] = HostUrl(host)
				},
				out var subject);

			emailService.Send(new EmailMessage() {
				Body = emailBody,
				Destination = envelope.Sender,
				Subject = subject
			});
		}

		public void SendUserInvitationEmail(string destination, string temporaryPassword, string loginUrl, string host) {
			EmailService emailService = new EmailService(_credentials);
			string emailBody = RenderTemplate(
				"user-invitation.html",
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
					["%%%login_url%%%"] = WebUtility.HtmlEncode(loginUrl),
					["%%%temporary_password%%%"] = WebUtility.HtmlEncode(temporaryPassword),
					["%%%url%%%"] = WebUtility.HtmlEncode(HostUrl(host))
				},
				out var subject);

			emailService.Send(new EmailMessage {
				Body = emailBody,
				Destination = destination,
				Subject = subject
			});
		}

		public void SendTwoFactorCodeEmail(string destination, string code, string host) {
			EmailService emailService = new EmailService(_credentials);
			string emailBody = RenderTemplate(
				"user-two-factor-code.html",
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
					["%%%verification_code%%%"] = WebUtility.HtmlEncode(code),
					["%%%url%%%"] = WebUtility.HtmlEncode(HostUrl(host))
				},
				out var subject);

			emailService.Send(new EmailMessage {
				Body = emailBody,
				Destination = destination,
				Subject = subject
			});
		}

		public void SendSignerEmailOtpEmail(Envelope envelope, Signer signer, string code, string host) {
			EmailService emailService = new EmailService(_credentials);
			string emailBody = RenderTemplate(
				"signing-email-otp.html",
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
					["%%%sender_name%%%"] = WebUtility.HtmlEncode(envelope.Sender),
					["%%%document_name%%%"] = WebUtility.HtmlEncode(envelope.Name),
					["%%%verification_code%%%"] = WebUtility.HtmlEncode(code),
					["%%%url%%%"] = WebUtility.HtmlEncode(HostUrl(host))
				},
				out var subject);

			emailService.Send(new EmailMessage {
				Body = emailBody,
				Destination = signer.Email,
				Subject = subject
			});
		}

		public void SendPasswordResetEmail(string destination, string resetUrl, string host) {
			EmailService emailService = new EmailService(_credentials);
			string emailBody = RenderTemplate(
				"user-password-reset.html",
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
					["%%%reset_url%%%"] = WebUtility.HtmlEncode(resetUrl),
					["%%%url%%%"] = WebUtility.HtmlEncode(HostUrl(host))
				},
				out var subject);

			emailService.Send(new EmailMessage {
				Body = emailBody,
				Destination = destination,
				Subject = subject
			});
		}

		private class EmailTemplateMetadata {
			public string Subject { get; set; }
			public string Preheader { get; set; }
		}

		private static string EncodeAccessId(params string[] parts) =>
			Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(string.Join(":", parts)))
				.TrimEnd('=')
				.Replace('+', '-')
				.Replace('/', '_');

		private static string GetRecipientUrl(string host, string envelopeCode, Envelope envelope, Signer signer) {
			if (signer.Role == RecipientRole.Approver) {
				return HostUrl(host) + "/envelopes/details/" + WebUtility.UrlEncode(envelope.EnvelopeID) + "?approvalId=" + WebUtility.UrlEncode(envelopeCode);
			}

			if (signer.Role == RecipientRole.Cc || signer.Role == RecipientRole.Observer) {
				return HostUrl(host) + "/envelopes/details/" + WebUtility.UrlEncode(envelope.EnvelopeID) + "?accessId=" + WebUtility.UrlEncode(envelopeCode);
			}

			var path = signer.Role switch {
				_ => "/review/sign"
			};

			return HostUrl(host) + path + "?id=" + WebUtility.UrlEncode(envelopeCode);
		}

		private static string GetRecipientHeading(Signer signer) =>
			signer.Role switch {
				RecipientRole.Approver => "Please review and approve",
				RecipientRole.Cc => "Document notification",
				RecipientRole.Observer => "Document available for review",
				_ => "Please review and sign"
			};

		private static string GetRecipientActionText(Signer signer) =>
			signer.Role switch {
				RecipientRole.Approver => "review and approve",
				RecipientRole.Cc => "keep for your records",
				RecipientRole.Observer => "review",
				_ => "review and sign"
			};

		private static string GetRecipientButtonText(Signer signer) =>
			signer.Role switch {
				RecipientRole.Approver => "REVIEW AND APPROVE",
				RecipientRole.Cc => "VIEW STATUS",
				RecipientRole.Observer => "VIEW DOCUMENT",
				_ => "REVIEW DOCUMENT"
			};

		private static bool HasStatusEvent(Signer signer, SignerStatus status) =>
			signer.StatusChanged?.Any(item => item.SignerStatus == status) == true;

	}
}

