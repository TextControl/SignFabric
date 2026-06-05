using SignFabric.Application.Services;
using SignFabric.Application.Abstractions;
using SignFabric.Application.ContractManagement;
using SignFabric.Application.Envelopes;
using SignFabric.Application.Signing;
using SignFabric.Application.Templates;
using SignFabric.Application.Contracts;
using SignFabric.Domain;
using SignFabric.Application.Identity;
using SignFabric.Presentation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Pages.Dashboard {
	/// <summary>
	/// Dashboard/Home page - displays overview and quick actions
	/// </summary>
	[Authorize]
	public class IndexModel : PageModel {
		private readonly IEnvelopeService _envelopeService;
		private readonly ITemplateService _templateService;
		private readonly ICertificateManagementService _certificateManagementService;
		private readonly ISignerDocumentService _signerDocumentService;
		private readonly string _userId;

		public List<Envelope> Envelopes { get; set; } = new();
		public List<Template> Templates { get; set; } = new();
		public int PendingSignatures { get; set; }
		public int CompletedDocuments { get; set; }
		public bool CanRequestSignatures { get; set; }
		public bool IsSignerAccount { get; set; }

		public IndexModel(
			IEnvelopeService envelopeService,
			ITemplateService templateService,
			ICertificateManagementService certificateManagementService,
			ISignerDocumentService signerDocumentService,
			ICurrentUserContext currentUserContext) {
			_envelopeService = envelopeService ?? throw new ArgumentNullException(nameof(envelopeService));
			_templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
			_certificateManagementService = certificateManagementService ?? throw new ArgumentNullException(nameof(certificateManagementService));
			_signerDocumentService = signerDocumentService ?? throw new ArgumentNullException(nameof(signerDocumentService));
			_userId = currentUserContext.UserId;
		}

		public async Task OnGetAsync() {
			try {
				IsSignerAccount = User.IsInRole(AppRoles.Signer);
				if (IsSignerAccount) {
					var signerEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name;
					Envelopes = await _signerDocumentService.GetSignedDocumentsAsync(signerEmail);
					CanRequestSignatures = false;
					CompletedDocuments = Envelopes.Count;
					return;
				}

				// Load user's envelopes
				Envelopes = await _envelopeService.GetAllAsync(_userId);
				
				// Load user's templates
				Templates = await _templateService.GetAllAsync(_userId);
				CanRequestSignatures = _certificateManagementService.HasActiveSigningCertificate();

				// Calculate statistics
				PendingSignatures = 0;
				CompletedDocuments = 0;

				foreach (var env in Envelopes) {
					if (env.Status == EnvelopeStatus.Sent) {
						PendingSignatures++;
					} else if (env.Status == EnvelopeStatus.Signed) {
						CompletedDocuments++;
					}
				}
			} catch (Exception ex) {
				TempData["Error"] = $"Error loading dashboard: {ex.Message}";
			}
		}
	}
}
