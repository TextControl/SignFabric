namespace SignFabric.Infrastructure.Configuration {
	public class BootstrapAdminOptions {
		public string Email { get; set; }

		public bool HasEmail => !string.IsNullOrWhiteSpace(Email);

		public bool Matches(string email) =>
			HasEmail &&
			string.Equals(Email.Trim(), email?.Trim(), System.StringComparison.OrdinalIgnoreCase);
	}
}
