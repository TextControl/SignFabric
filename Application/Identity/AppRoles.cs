namespace SignFabric.Application.Identity {
	public static class AppRoles {
		public const string Admin = "Admin";
		public const string User = "User";
		public const string Signer = "Signer";
		public const string EnvelopeCreators = Admin + "," + User;

		public static readonly string[] All = {
			Admin,
			User,
			Signer
		};
	}
}
