using SignFabric.Infrastructure.Configuration;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Email.Legacy {
	public class EmailService {

		private readonly Credentials _credentials;

		public EmailService(Credentials credentials) {
			_credentials = credentials;
		}

		public void Send(EmailMessage message) {
			configSMTPasync(message);
		}

		// send email via smtp service
		private void configSMTPasync(EmailMessage message) {

			// Configure the client:
			System.Net.Mail.SmtpClient client = new System.Net.Mail.SmtpClient(_credentials.Email.Server);

			client.Port = _credentials.Email.Port;
			client.DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network;
			client.UseDefaultCredentials = false;

			// Creatte the credentials:
			System.Net.NetworkCredential credentials = new System.Net.NetworkCredential(_credentials.Email.Username, _credentials.Email.Password);
			client.EnableSsl = true;
			client.Credentials = credentials;

			// Create the message:
			var mail = new System.Net.Mail.MailMessage(_credentials.Email.From, message.Destination);
			mail.Subject = message.Subject;
			mail.Body = message.Body;
			mail.IsBodyHtml = true;

			foreach (var bccAddress in SplitRecipients(_credentials.Email.Bcc)) {
				mail.Bcc.Add(bccAddress);
			}

            foreach (Attachment attachment in message.Attachments) {
				mail.Attachments.Add(attachment);
			}

			client.Send(mail);
		}

		private static IEnumerable<string> SplitRecipients(string recipients) {
			if (string.IsNullOrWhiteSpace(recipients)) {
				return Enumerable.Empty<string>();
			}

			return recipients
				.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(recipient => recipient.Trim())
				.Where(recipient => !string.IsNullOrWhiteSpace(recipient));
		}
	}

	public class EmailMessage {
		public string Destination { get; set; }
		public string Subject { get; set; }
		public string Body { get; set; }
		public List<Attachment> Attachments { get; set; } = new List<Attachment>();
	}
}
