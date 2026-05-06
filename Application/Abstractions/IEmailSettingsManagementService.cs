using System.Collections.Generic;
using System.Threading.Tasks;

namespace SignFabric.Application.Abstractions {
	public interface IEmailSettingsManagementService {
		Task<EmailSettingsConfiguration> GetSettingsAsync();
		Task SaveSettingsAsync(EmailSettingsConfiguration settings);
		Task<IReadOnlyList<EmailTemplateSummary>> GetTemplatesAsync();
		Task<EmailTemplateDetails> GetTemplateAsync(string fileName);
		Task SaveTemplateAsync(string fileName, string html, string subject, string preheader);
	}

	public class EmailSettingsConfiguration {
		public string Username { get; set; }
		public string Password { get; set; }
		public string From { get; set; }
		public string Bcc { get; set; }
		public string Server { get; set; }
		public int Port { get; set; }
		public bool HasPassword { get; set; }
		public bool IsPasswordProtected { get; set; }
		public bool PasswordRequiresReset { get; set; }
	}

	public class EmailTemplateSummary {
		public string FileName { get; set; }
		public string DisplayName { get; set; }
		public string Subject { get; set; }
		public IReadOnlyList<string> Placeholders { get; set; } = new List<string>();
	}

	public class EmailTemplateDetails {
		public string FileName { get; set; }
		public string Html { get; set; }
		public string Subject { get; set; }
		public string Preheader { get; set; }
		public IReadOnlyList<string> Placeholders { get; set; } = new List<string>();
	}
}
