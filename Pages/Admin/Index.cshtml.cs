using SignFabric.Application.Abstractions;
using SignFabric.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SignFabric.Pages.Admin {
	[Authorize(Roles = AppRoles.Admin)]
	public class IndexModel : PageModel {
		private readonly IUserAdministrationService _userAdministrationService;
		private readonly ICertificateManagementService _certificateManagementService;
		private readonly IEmailSettingsManagementService _emailSettingsManagementService;
		private readonly IAuthenticationSettingsManagementService _authenticationSettingsManagementService;
		private readonly ICurrentUserContext _currentUserContext;
		private readonly IEmailSender _emailSender;

		public IReadOnlyList<UserSummary> Users { get; set; } = new List<UserSummary>();
		public int TotalUsers { get; set; }
		public int TotalAdmins { get; set; }
		public int TotalStandardUsers { get; set; }
		public int TotalDisabledUsers { get; set; }
		public string[] AvailableRoles => AppRoles.All;
		public IReadOnlyList<SigningCertificateSummary> Certificates { get; set; } = new List<SigningCertificateSummary>();
		public SigningCertificateConfiguration CertificateConfiguration { get; set; } = new();
		public IReadOnlyList<EmailTemplateSummary> EmailTemplates { get; set; } = new List<EmailTemplateSummary>();
		public EmailTemplateDetails SelectedEmailTemplate { get; set; } = new();
		public bool HasEmailCredentials { get; set; }
		public AuthenticationSettingsConfiguration AuthenticationConfiguration { get; set; } = new();
		public bool ShowInviteUserModal { get; set; }
		public bool ShowUploadCertificateModal { get; set; }
		public bool ShowCreateApiClientModal { get; set; }

		[TempData]
		public string StatusMessage { get; set; }

		[TempData]
		public string ActiveAdminTab { get; set; }

		[TempData]
		public string CreatedLocalOAuthClientId { get; set; }

		[TempData]
		public string CreatedLocalOAuthClientSecret { get; set; }

		[BindProperty]
		public InviteUserInput InviteUser { get; set; } = new();

		[BindProperty]
		public UploadCertificateInput UploadCertificate { get; set; } = new();

		[BindProperty]
		public AzureKeyVaultInput AzureKeyVault { get; set; } = new();

		[BindProperty]
		public EmailSettingsInput EmailSettings { get; set; } = new();

		[BindProperty]
		public EmailTemplateInput EmailTemplate { get; set; } = new();

		[BindProperty]
		public AuthenticationSettingsInput AuthenticationSettings { get; set; } = new();

		[BindProperty]
		public CreateLocalOAuthClientInput LocalOAuthClient { get; set; } = new();

		public IndexModel(
			IUserAdministrationService userAdministrationService,
			ICertificateManagementService certificateManagementService,
			IEmailSettingsManagementService emailSettingsManagementService,
			IAuthenticationSettingsManagementService authenticationSettingsManagementService,
			ICurrentUserContext currentUserContext,
			IEmailSender emailSender) {
			_userAdministrationService = userAdministrationService;
			_certificateManagementService = certificateManagementService;
			_emailSettingsManagementService = emailSettingsManagementService;
			_authenticationSettingsManagementService = authenticationSettingsManagementService;
			_currentUserContext = currentUserContext;
			_emailSender = emailSender;
		}

		public async Task OnGetAsync(string emailTemplate = null) {
			await LoadAsync(emailTemplate);
		}

		public async Task<IActionResult> OnPostInviteAsync() {
			ActiveAdminTab = "users";
			ModelState.Clear();
			TryValidateModel(InviteUser, nameof(InviteUser));

			if (!ModelState.IsValid) {
				ShowInviteUserModal = true;
				await LoadAsync();
				return Page();
			}

			try {
				await _userAdministrationService.InviteUserAsync(
					InviteUser.Email,
					InviteUser.FirstName,
					InviteUser.LastName,
					InviteUser.TemporaryPassword,
					InviteUser.Role,
					InviteUser.RequireTwoFactor);

				if (InviteUser.SendInvitationEmail) {
					var loginUrl = Url.Page(
						"/Account/Login",
						pageHandler: null,
						values: new { area = "Identity" },
						protocol: Request.Scheme,
						host: Request.Host.ToString());

					await _emailSender.SendUserInvitationAsync(
						InviteUser.Email,
						InviteUser.TemporaryPassword,
						loginUrl);
				}

				StatusMessage = InviteUser.SendInvitationEmail
					? "User account created and invitation e-mail sent."
					: "User invitation account created.";
				return RedirectToPage();
			}
			catch (Exception ex) {
				ModelState.AddModelError(string.Empty, ex.Message);
				await LoadAsync();
				return Page();
			}
		}

		public async Task<IActionResult> OnPostSetRoleAsync(string userId, string role) {
			ActiveAdminTab = "users";
			try {
				await _userAdministrationService.SetRoleAsync(userId, role);
				StatusMessage = "User role updated.";
			}
			catch (Exception ex) {
				StatusMessage = ex.Message;
			}

			return RedirectToPage();
		}

		public async Task<IActionResult> OnPostCreateLocalOAuthClientAsync() {
			ActiveAdminTab = "auth";
			ModelState.Clear();
			TryValidateModel(LocalOAuthClient, nameof(LocalOAuthClient));

			var scopes = new List<string>();
			if (LocalOAuthClient.AllowEnvelopeCreate) {
				scopes.Add("envelopes:create");
			}
			if (LocalOAuthClient.AllowEnvelopeRead) {
				scopes.Add("envelopes:read");
			}
			if (!scopes.Any()) {
				ModelState.AddModelError("LocalOAuthClient.AllowEnvelopeCreate", "Select at least one API permission.");
			}

			if (!ModelState.IsValid) {
				ShowCreateApiClientModal = true;
				await LoadAsync(EmailTemplate.FileName);
				return Page();
			}

			var settings = await _authenticationSettingsManagementService.GetSettingsAsync();
			var clientId = string.IsNullOrWhiteSpace(LocalOAuthClient.ClientId)
				? $"sf_{GenerateUrlSafeToken(18)}"
				: LocalOAuthClient.ClientId.Trim();

			if (settings.LocalOAuth.Clients.Any(client => string.Equals(client.ClientId, clientId, StringComparison.OrdinalIgnoreCase))) {
				ModelState.AddModelError("LocalOAuthClient.ClientId", "A local OAuth client with this client id already exists.");
				ShowCreateApiClientModal = true;
				await LoadAsync(EmailTemplate.FileName);
				return Page();
			}

			var clientSecret = $"sfsecret_{GenerateUrlSafeToken(32)}";
			await _authenticationSettingsManagementService.AddLocalOAuthClientAsync(new LocalOAuthClientSettings {
				ClientId = clientId,
				DisplayName = LocalOAuthClient.DisplayName?.Trim(),
				SecretSha256 = HashSecret(clientSecret),
				UserId = LocalOAuthClient.UserId,
				Scopes = scopes
			}, settings.LocalOAuth.HasSigningKey ? null : GenerateUrlSafeToken(48));

			CreatedLocalOAuthClientId = clientId;
			CreatedLocalOAuthClientSecret = clientSecret;
			StatusMessage = "API client created. Copy the client secret now; it will not be shown again.";

			return RedirectToPage();
		}

		public async Task<IActionResult> OnPostDeleteLocalOAuthClientAsync(string clientId) {
			ActiveAdminTab = "auth";
			if (string.IsNullOrWhiteSpace(clientId)) {
				StatusMessage = "Select an API client to delete.";
				return RedirectToPage();
			}

			try {
				var removed = await _authenticationSettingsManagementService.DeleteLocalOAuthClientAsync(clientId);

				if (!removed) {
					StatusMessage = "The API client could not be found.";
				}
				else {
					StatusMessage = "API client deleted.";
				}
			}
			catch (Exception ex) {
				StatusMessage = ex.Message;
			}

			return RedirectToPage();
		}

		public async Task<IActionResult> OnPostSetEnabledAsync(string userId, bool enabled) {
			ActiveAdminTab = "users";
			try {
				await _userAdministrationService.SetEnabledAsync(userId, enabled, _currentUserContext.UserId);
				StatusMessage = enabled ? "User enabled." : "User disabled.";
			}
			catch (Exception ex) {
				StatusMessage = ex.Message;
			}

			return RedirectToPage();
		}

		public async Task<IActionResult> OnPostSetTwoFactorAsync(string userId, bool enabled) {
			ActiveAdminTab = "users";
			try {
				await _userAdministrationService.SetTwoFactorEnabledAsync(userId, enabled);
				StatusMessage = enabled ? "E-mail two-factor authentication enabled." : "E-mail two-factor authentication disabled.";
			}
			catch (Exception ex) {
				StatusMessage = ex.Message;
			}

			return RedirectToPage();
		}

		public async Task<IActionResult> OnPostDeleteUserAsync(string userId) {
			ActiveAdminTab = "users";
			try {
				await _userAdministrationService.DeleteUserAsync(userId, _currentUserContext.UserId);
				StatusMessage = "User deleted.";
			}
			catch (Exception ex) {
				StatusMessage = ex.Message;
			}

			return RedirectToPage();
		}

		public async Task<IActionResult> OnPostUploadCertificateAsync() {
			ActiveAdminTab = "certificates";
			ModelState.Clear();
			TryValidateModel(UploadCertificate, nameof(UploadCertificate));

			if (UploadCertificate.File == null || UploadCertificate.File.Length == 0) {
				ModelState.AddModelError("UploadCertificate.File", "Select a PFX file.");
			}

			if (!ModelState.IsValid) {
				if (IsAjaxRequest()) {
					return BadRequest(new {
						success = false,
						errors = GetModelErrors()
					});
				}

				ShowUploadCertificateModal = true;
				await LoadAsync();
				return Page();
			}

			try {
				await using var stream = UploadCertificate.File.OpenReadStream();
				await _certificateManagementService.UploadLocalPfxAsync(
					UploadCertificate.DisplayName,
					UploadCertificate.File.FileName,
					stream,
					UploadCertificate.Password);

				StatusMessage = "Certificate uploaded.";
			}
			catch (Exception ex) {
				if (IsAjaxRequest()) {
					return BadRequest(new {
						success = false,
						errors = new[] { ex.Message }
					});
				}

				ModelState.AddModelError(string.Empty, ex.Message);
				ShowUploadCertificateModal = true;
				await LoadAsync();
				return Page();
			}

			if (IsAjaxRequest()) {
				return new JsonResult(new {
					success = true,
					redirectUrl = Url.Page("/Admin/Index")
				});
			}

			return RedirectToPage();
		}

		private bool IsAjaxRequest() =>
			string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

		private string[] GetModelErrors() =>
			ModelState.Values
				.SelectMany(entry => entry.Errors)
				.Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The upload could not be completed." : error.ErrorMessage)
				.ToArray();

		public async Task<IActionResult> OnPostActivateCertificateAsync(string certificateId) {
			ActiveAdminTab = "certificates";
			try {
				await _certificateManagementService.ActivateLocalPfxAsync(certificateId);
				StatusMessage = "Signing certificate activated.";
			}
			catch (Exception ex) {
				StatusMessage = ex.Message;
			}

			return RedirectToPage();
		}

		public async Task<IActionResult> OnPostUseLocalPfxAsync() {
			ActiveAdminTab = "certificates";
			try {
				await _certificateManagementService.UseLocalPfxAsync();
				StatusMessage = "Local PFX signing certificate activated.";
			}
			catch (Exception ex) {
				StatusMessage = ex.Message;
			}

			return RedirectToPage();
		}

		public async Task<IActionResult> OnPostConfigureAzureKeyVaultAsync() {
			ActiveAdminTab = "certificates";
			ModelState.Clear();
			TryValidateModel(AzureKeyVault, nameof(AzureKeyVault));

			if (!ModelState.IsValid) {
				await LoadAsync();
				return Page();
			}

			try {
				await _certificateManagementService.ConfigureAzureKeyVaultAsync(
					AzureKeyVault.VaultUri,
					AzureKeyVault.CertificateName,
					AzureKeyVault.UseDefaultAzureCredential,
					AzureKeyVault.TenantId,
					AzureKeyVault.ClientId);

				StatusMessage = "Azure Key Vault signing certificate activated.";
			}
			catch (Exception ex) {
				StatusMessage = ex.Message;
			}

			return RedirectToPage();
		}

		public async Task<IActionResult> OnPostDeleteCertificateAsync(string certificateId) {
			ActiveAdminTab = "certificates";
			try {
				await _certificateManagementService.DeleteLocalPfxAsync(certificateId);
				StatusMessage = "Certificate deleted.";
			}
			catch (Exception ex) {
				StatusMessage = ex.Message;
			}

			return RedirectToPage();
		}

		public async Task<IActionResult> OnPostSaveEmailSettingsAsync() {
			ActiveAdminTab = "email";
			ModelState.Clear();
			TryValidateModel(EmailSettings, nameof(EmailSettings));

			if (!ModelState.IsValid) {
				await LoadAsync(EmailTemplate.FileName);
				return Page();
			}

			try {
				await _emailSettingsManagementService.SaveSettingsAsync(new EmailSettingsConfiguration {
					Username = EmailSettings.Username,
					Password = EmailSettings.Password,
					From = EmailSettings.From,
					Bcc = EmailSettings.Bcc,
					Server = EmailSettings.Server,
					Port = EmailSettings.Port
				});
				StatusMessage = "E-mail settings saved.";
			}
			catch (Exception ex) {
				StatusMessage = ex.Message;
			}

			return RedirectToEmailTemplate(EmailTemplate.FileName);
		}

		public async Task<IActionResult> OnPostSaveEmailTemplateAsync() {
			ActiveAdminTab = "email";
			ModelState.Clear();
			TryValidateModel(EmailTemplate, nameof(EmailTemplate));

			if (!ModelState.IsValid) {
				await LoadAsync(EmailTemplate.FileName);
				return Page();
			}

			try {
				await _emailSettingsManagementService.SaveTemplateAsync(
					EmailTemplate.FileName,
					EmailTemplate.Html,
					EmailTemplate.Subject,
					EmailTemplate.Preheader);
				StatusMessage = "E-mail template saved.";
			}
			catch (Exception ex) {
				StatusMessage = ex.Message;
			}

			return RedirectToEmailTemplate(EmailTemplate.FileName);
		}

		public async Task<IActionResult> OnPostSaveAuthenticationSettingsAsync() {
			ActiveAdminTab = "auth";
			ModelState.Clear();
			TryValidateModel(AuthenticationSettings, nameof(AuthenticationSettings));

			if (!ModelState.IsValid) {
				await LoadAsync(EmailTemplate.FileName);
				return Page();
			}

			try {
				var currentSettings = await _authenticationSettingsManagementService.GetSettingsAsync();
				await _authenticationSettingsManagementService.SaveSettingsAsync(new AuthenticationSettingsConfiguration {
					Bearer = new BearerAuthenticationSettings {
						Authority = AuthenticationSettings.BearerAuthority,
						Audience = AuthenticationSettings.BearerAudience,
						RequireHttpsMetadata = AuthenticationSettings.BearerRequireHttpsMetadata
					},
					OpenIdConnect = new OpenIdConnectAuthenticationSettings {
						Enabled = AuthenticationSettings.OpenIdConnectEnabled,
						DisplayName = AuthenticationSettings.OpenIdConnectDisplayName,
						Authority = AuthenticationSettings.OpenIdConnectAuthority,
						ClientId = AuthenticationSettings.OpenIdConnectClientId,
						ClientSecret = AuthenticationSettings.OpenIdConnectClientSecret,
						CallbackPath = AuthenticationSettings.OpenIdConnectCallbackPath,
						SignedOutCallbackPath = AuthenticationSettings.OpenIdConnectSignedOutCallbackPath,
						ResponseType = AuthenticationSettings.OpenIdConnectResponseType,
						SaveTokens = AuthenticationSettings.OpenIdConnectSaveTokens,
						AutoProvisionUsers = AuthenticationSettings.OpenIdConnectAutoProvisionUsers,
						Scopes = SplitValues(AuthenticationSettings.OpenIdConnectScopes)
					},
					LocalOAuth = new LocalOAuthSettings {
						Enabled = AuthenticationSettings.LocalOAuthEnabled,
						Issuer = AuthenticationSettings.LocalOAuthIssuer,
						Audience = AuthenticationSettings.LocalOAuthAudience,
						SigningKey = AuthenticationSettings.LocalOAuthSigningKey,
						AccessTokenMinutes = AuthenticationSettings.LocalOAuthAccessTokenMinutes,
						Clients = currentSettings.LocalOAuth.Clients
					}
				});
				StatusMessage = "Authentication settings saved. Restart the application for authentication scheme changes to take effect.";
			}
			catch (Exception ex) {
				StatusMessage = ex.Message;
			}

			return RedirectToPage();
		}

		private IActionResult RedirectToEmailTemplate(string emailTemplate) =>
			Redirect($"/admin?emailTemplate={Uri.EscapeDataString(emailTemplate ?? string.Empty)}");

		private async Task LoadAsync(string selectedEmailTemplate = null) {
			if (string.IsNullOrWhiteSpace(InviteUser.TemporaryPassword)) {
				InviteUser.TemporaryPassword = GenerateTemporaryPassword();
			}

			Users = await _userAdministrationService.GetUsersAsync();
			TotalUsers = Users.Count;
			TotalAdmins = Users.Count(user => user.Roles.Contains(AppRoles.Admin));
			TotalStandardUsers = Users.Count(user => user.Roles.Contains(AppRoles.User));
			TotalDisabledUsers = Users.Count(user => user.IsDisabled);
			Certificates = await _certificateManagementService.GetCertificatesAsync();
			CertificateConfiguration = await _certificateManagementService.GetConfigurationAsync();
			AzureKeyVault = new AzureKeyVaultInput {
				VaultUri = CertificateConfiguration.AzureKeyVaultUri,
				CertificateName = CertificateConfiguration.AzureCertificateName,
				UseDefaultAzureCredential = CertificateConfiguration.AzureUseDefaultAzureCredential,
				TenantId = CertificateConfiguration.AzureTenantId,
				ClientId = CertificateConfiguration.AzureClientId
			};

			var settings = await _emailSettingsManagementService.GetSettingsAsync();
			EmailSettings = new EmailSettingsInput {
				Username = settings.Username,
				From = settings.From,
				Bcc = settings.Bcc,
				Server = settings.Server,
				Port = settings.Port,
				HasPassword = settings.HasPassword,
				IsPasswordProtected = settings.IsPasswordProtected,
				PasswordRequiresReset = settings.PasswordRequiresReset
			};
			HasEmailCredentials =
				!string.IsNullOrWhiteSpace(settings.Server) &&
				settings.Port > 0 &&
				!string.IsNullOrWhiteSpace(settings.From) &&
				!string.IsNullOrWhiteSpace(settings.Username) &&
				settings.HasPassword;

			AuthenticationConfiguration = await _authenticationSettingsManagementService.GetSettingsAsync();
			AuthenticationSettings = new AuthenticationSettingsInput {
				BearerAuthority = AuthenticationConfiguration.Bearer.Authority,
				BearerAudience = AuthenticationConfiguration.Bearer.Audience,
				BearerRequireHttpsMetadata = AuthenticationConfiguration.Bearer.RequireHttpsMetadata,
				OpenIdConnectEnabled = AuthenticationConfiguration.OpenIdConnect.Enabled,
				OpenIdConnectDisplayName = AuthenticationConfiguration.OpenIdConnect.DisplayName,
				OpenIdConnectAuthority = AuthenticationConfiguration.OpenIdConnect.Authority,
				OpenIdConnectClientId = AuthenticationConfiguration.OpenIdConnect.ClientId,
				OpenIdConnectCallbackPath = AuthenticationConfiguration.OpenIdConnect.CallbackPath,
				OpenIdConnectSignedOutCallbackPath = AuthenticationConfiguration.OpenIdConnect.SignedOutCallbackPath,
				OpenIdConnectResponseType = AuthenticationConfiguration.OpenIdConnect.ResponseType,
				OpenIdConnectSaveTokens = AuthenticationConfiguration.OpenIdConnect.SaveTokens,
				OpenIdConnectAutoProvisionUsers = AuthenticationConfiguration.OpenIdConnect.AutoProvisionUsers,
				OpenIdConnectScopes = string.Join(Environment.NewLine, AuthenticationConfiguration.OpenIdConnect.Scopes),
				OpenIdConnectHasClientSecret = AuthenticationConfiguration.OpenIdConnect.HasClientSecret,
				LocalOAuthEnabled = AuthenticationConfiguration.LocalOAuth.Enabled,
				LocalOAuthIssuer = AuthenticationConfiguration.LocalOAuth.Issuer,
				LocalOAuthAudience = AuthenticationConfiguration.LocalOAuth.Audience,
				LocalOAuthAccessTokenMinutes = AuthenticationConfiguration.LocalOAuth.AccessTokenMinutes,
				LocalOAuthHasSigningKey = AuthenticationConfiguration.LocalOAuth.HasSigningKey
			};

			EmailTemplates = await _emailSettingsManagementService.GetTemplatesAsync();
			var templateFileName = string.IsNullOrWhiteSpace(selectedEmailTemplate)
				? EmailTemplates.FirstOrDefault()?.FileName
				: selectedEmailTemplate;

			if (!string.IsNullOrWhiteSpace(templateFileName)) {
				SelectedEmailTemplate = await _emailSettingsManagementService.GetTemplateAsync(templateFileName);
				EmailTemplate = new EmailTemplateInput {
					FileName = SelectedEmailTemplate.FileName,
					Html = SelectedEmailTemplate.Html,
					Subject = SelectedEmailTemplate.Subject,
					Preheader = SelectedEmailTemplate.Preheader
				};
			}
		}

		private static string GenerateTemporaryPassword() {
			const string lower = "abcdefghijkmnopqrstuvwxyz";
			const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
			const string digits = "23456789";
			const string symbols = "!$%";
			var required = new[] {
				GetRandomCharacter(lower),
				GetRandomCharacter(upper),
				GetRandomCharacter(digits),
				GetRandomCharacter(symbols)
			};
			var all = lower + upper + digits + symbols;
			var password = required
				.Concat(Enumerable.Range(0, 8).Select(_ => GetRandomCharacter(all)))
				.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue))
				.ToArray();

			return new string(password);
		}

		private static char GetRandomCharacter(string characters) =>
			characters[RandomNumberGenerator.GetInt32(characters.Length)];

		private static string GenerateUrlSafeToken(int byteCount) {
			var bytes = RandomNumberGenerator.GetBytes(byteCount);
			return Convert.ToBase64String(bytes)
				.TrimEnd('=')
				.Replace('+', '-')
				.Replace('/', '_');
		}

		private static string HashSecret(string secret) {
			var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
			return Convert.ToHexString(bytes).ToLowerInvariant();
		}

		private static List<string> SplitValues(string value) =>
			(value ?? string.Empty)
				.Split(new[] { '\r', '\n', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

		public class InviteUserInput {
			[Display(Name = "First Name")]
			public string FirstName { get; set; }

			[Display(Name = "Name")]
			public string LastName { get; set; }

			[Required]
			[EmailAddress]
			[Display(Name = "E-Mail")]
			public string Email { get; set; }

			[Required]
			[StringLength(100, MinimumLength = 8)]
			[DataType(DataType.Password)]
			[Display(Name = "Temporary Password")]
			public string TemporaryPassword { get; set; }

			[Required]
			public string Role { get; set; } = AppRoles.User;

			[Display(Name = "Send invitation e-mail")]
			public bool SendInvitationEmail { get; set; }

			[Display(Name = "Require e-mail two-factor authentication")]
			public bool RequireTwoFactor { get; set; } = true;
		}

		public class UploadCertificateInput {
			[Required]
			[Display(Name = "Display Name")]
			public string DisplayName { get; set; }

			[Required]
			[Display(Name = "PFX File")]
			public IFormFile File { get; set; }

			[Display(Name = "PFX Password")]
			[DataType(DataType.Password)]
			public string Password { get; set; }
		}

		public class AzureKeyVaultInput {
			[Required]
			[Url]
			[Display(Name = "Vault URI")]
			public string VaultUri { get; set; }

			[Required]
			[Display(Name = "Certificate Name")]
			public string CertificateName { get; set; }

			[Display(Name = "Use default Azure credential")]
			public bool UseDefaultAzureCredential { get; set; } = true;

			[Display(Name = "Tenant ID")]
			public string TenantId { get; set; }

			[Display(Name = "Client ID")]
			public string ClientId { get; set; }
		}

		public class EmailSettingsInput {
			[Display(Name = "SMTP username")]
			public string Username { get; set; }

			[DataType(DataType.Password)]
			[Display(Name = "SMTP password")]
			public string Password { get; set; }

			[Required]
			[EmailAddress]
			[Display(Name = "From address")]
			public string From { get; set; }

			[Display(Name = "BCC recipients")]
			public string Bcc { get; set; }

			[Required]
			[Display(Name = "SMTP server")]
			public string Server { get; set; }

			[Range(1, 65535)]
			[Display(Name = "SMTP port")]
			public int Port { get; set; } = 587;

			public bool HasPassword { get; set; }
			public bool IsPasswordProtected { get; set; }
			public bool PasswordRequiresReset { get; set; }
		}

		public class EmailTemplateInput {
			[Required]
			public string FileName { get; set; }

			[Required]
			[Display(Name = "E-mail subject")]
			public string Subject { get; set; }

			[Display(Name = "Preheader")]
			public string Preheader { get; set; }

			[Required]
			[Display(Name = "HTML")]
			public string Html { get; set; }
		}

		public class AuthenticationSettingsInput {
			[Display(Name = "External bearer authority")]
			public string BearerAuthority { get; set; }

			[Display(Name = "External bearer audience")]
			public string BearerAudience { get; set; }

			[Display(Name = "Require HTTPS metadata")]
			public bool BearerRequireHttpsMetadata { get; set; } = true;

			[Display(Name = "Enable OpenID Connect")]
			public bool OpenIdConnectEnabled { get; set; }

			[Display(Name = "Button label")]
			public string OpenIdConnectDisplayName { get; set; }

			[Display(Name = "Authority")]
			public string OpenIdConnectAuthority { get; set; }

			[Display(Name = "Client ID")]
			public string OpenIdConnectClientId { get; set; }

			[DataType(DataType.Password)]
			[Display(Name = "Client secret")]
			public string OpenIdConnectClientSecret { get; set; }

			public bool OpenIdConnectHasClientSecret { get; set; }

			[Display(Name = "Callback path")]
			public string OpenIdConnectCallbackPath { get; set; }

			[Display(Name = "Signed-out callback path")]
			public string OpenIdConnectSignedOutCallbackPath { get; set; }

			[Display(Name = "Response type")]
			public string OpenIdConnectResponseType { get; set; }

			[Display(Name = "Save tokens")]
			public bool OpenIdConnectSaveTokens { get; set; } = true;

			[Display(Name = "Auto-provision users")]
			public bool OpenIdConnectAutoProvisionUsers { get; set; }

			[Display(Name = "Scopes")]
			public string OpenIdConnectScopes { get; set; }

			[Display(Name = "Enable local OAuth")]
			public bool LocalOAuthEnabled { get; set; }

			[Display(Name = "Issuer")]
			public string LocalOAuthIssuer { get; set; }

			[Display(Name = "Audience")]
			public string LocalOAuthAudience { get; set; }

			[DataType(DataType.Password)]
			[Display(Name = "Signing key")]
			public string LocalOAuthSigningKey { get; set; }

			public bool LocalOAuthHasSigningKey { get; set; }

			[Range(1, 1440)]
			[Display(Name = "Access token lifetime in minutes")]
			public int LocalOAuthAccessTokenMinutes { get; set; } = 60;

		}

		public class CreateLocalOAuthClientInput {
			[Required]
			[Display(Name = "Display name")]
			public string DisplayName { get; set; }

			[RegularExpression("^[A-Za-z0-9._~-]+$", ErrorMessage = "Use only letters, numbers, dots, underscores, tildes, or hyphens.")]
			[Display(Name = "Client ID")]
			public string ClientId { get; set; }

			[Required]
			[Display(Name = "Envelope owner")]
			public string UserId { get; set; }

			[Display(Name = "Create envelopes")]
			public bool AllowEnvelopeCreate { get; set; } = true;

			[Display(Name = "Read envelopes")]
			public bool AllowEnvelopeRead { get; set; } = true;
		}
	}
}
