using System.Collections.Generic;
using System.Linq;

namespace SignFabric.Application.Identity {
	public class UserSummary {
		public string Id { get; set; }
		public string Email { get; set; }
		public string UserName { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string DisplayName => string.Join(" ", new[] { FirstName, LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
		public bool EmailConfirmed { get; set; }
		public bool TwoFactorEnabled { get; set; }
		public bool IsDisabled { get; set; }
		public IReadOnlyList<string> Roles { get; set; } = new List<string>();
	}
}
