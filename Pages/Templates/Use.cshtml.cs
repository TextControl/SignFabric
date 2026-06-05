using SignFabric.Application.Services;
using SignFabric.Application.Abstractions;
using SignFabric.Application.ContractManagement;
using SignFabric.Application.Envelopes;
using SignFabric.Application.Signing;
using SignFabric.Application.Templates;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Presentation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Pages.Templates {
	[Authorize(Roles = SignFabric.Application.Identity.AppRoles.EnvelopeCreators)]
	public class UseModel : PageModel {
		private readonly IDocumentMergeService _mergeService;
		private readonly ITemplateService _templateService;
		private readonly IEnvelopeService _envelopeService;
		private readonly ICertificateManagementService _certificateManagementService;
		private readonly string _userId;
		private readonly string _userName;

		[BindProperty]
		public string EnvelopeName { get; set; }

		[BindProperty]
		public string RecipientEmails { get; set; }

		[BindProperty]
		public string SigningCertificateId { get; set; }

		public Template Template { get; set; }
		public string ErrorMessage { get; set; }
		public bool CanRequestSignatures { get; set; }
		public IReadOnlyList<SigningCertificateSummary> Certificates { get; set; } = new List<SigningCertificateSummary>();
		public string DefaultCertificateId { get; set; }

		public UseModel(
			IDocumentMergeService mergeService,
			ITemplateService templateService,
			IEnvelopeService envelopeService,
			ICertificateManagementService certificateManagementService,
			ICurrentUserContext currentUserContext) {
			_mergeService = mergeService ?? throw new ArgumentNullException(nameof(mergeService));
			_templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
			_envelopeService = envelopeService ?? throw new ArgumentNullException(nameof(envelopeService));
			_certificateManagementService = certificateManagementService ?? throw new ArgumentNullException(nameof(certificateManagementService));
			
			_userId = currentUserContext.UserId;
			_userName = currentUserContext.UserName;
		}

		public async Task<IActionResult> OnGetAsync(string id) {
			try {
				if (string.IsNullOrEmpty(id)) {
					return NotFound();
				}

				Template = await _templateService.GetAsync(id);
				if (Template == null) {
					return NotFound();
				}

				await LoadCertificateStateAsync();

				return Page();
			} catch (Exception ex) {
				ErrorMessage = $"Error loading template: {ex.Message}";
				return Page();
			}
		}

		public async Task<IActionResult> OnPostAsync(string id) {
			try {
				if (string.IsNullOrEmpty(id)) {
					return NotFound();
				}

				Template = await _templateService.GetAsync(id);
				if (Template == null) {
					return NotFound();
				}

				await LoadCertificateStateAsync();
				if (!CanRequestSignatures) {
					ErrorMessage = "A signing certificate is required to request signatures. Upload and activate a local PFX certificate or configure Azure Key Vault in the admin portal.";
					return Page();
				}

				if (string.IsNullOrWhiteSpace(EnvelopeName)) {
					ErrorMessage = "Please provide an envelope name.";
					return Page();
				}

				// Create envelope from template
				var envelope = new Envelope {
					EnvelopeID = Guid.NewGuid().ToString(),
					Name = EnvelopeName,
					UserID = _userId,
					Sender = _userName,
					Created = DateTime.Now,
					SigningCertificateId = string.IsNullOrWhiteSpace(SigningCertificateId) ? DefaultCertificateId : SigningCertificateId.Trim(),
					Status = EnvelopeStatus.New
				};

				// Add recipients if provided
				if (!string.IsNullOrWhiteSpace(RecipientEmails)) {
					var emails = RecipientEmails.Split(new[] { ',', ';', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
					foreach (var email in emails) {
						envelope.Signers.Add(new Signer {
							Id = Guid.NewGuid().ToString(),
							Email = email.Trim(),
							SignerStatus = SignerStatus.Sent
						});
					}
				}

				await _envelopeService.UpdateAsync(envelope);

				return RedirectToPage("/Envelopes/Details", new { id = envelope.EnvelopeID });
			} catch (Exception ex) {
				ErrorMessage = $"Error creating envelope from template: {ex.Message}";
				return Page();
			}
		}

		private async Task LoadCertificateStateAsync() {
			CanRequestSignatures = _certificateManagementService.HasActiveSigningCertificate();
			Certificates = await _certificateManagementService.GetCertificatesAsync();
			DefaultCertificateId = _certificateManagementService.GetDefaultLocalCertificateId();
		}
	}
}
