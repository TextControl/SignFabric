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
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SignFabric.Pages.Envelopes {
	/// <summary>
	/// Envelopes Create Page - prepare document for signing
	/// This is a multi-step form that uses JavaScript/AJAX for interaction
	/// </summary>
	[Authorize(Roles = SignFabric.Application.Identity.AppRoles.EnvelopeCreators)]
	public class CreateModel : PageModel {
		private readonly IDocumentPageService _pageService;
		private readonly IStoreRepositoryFactory _storeFactory;
		private readonly IEnvelopeDocumentFactory _envelopeDocumentFactory;
		private readonly ICertificateManagementService _certificateManagementService;
		private readonly string _userId;

		public Envelope Envelope { get; set; }
		public string CertificateRequiredMessage { get; set; }
		public IReadOnlyList<SigningCertificateSummary> Certificates { get; set; } = new List<SigningCertificateSummary>();
		public string DefaultCertificateId { get; set; }

		[BindProperty(SupportsGet = true)]
		public string Id { get; set; }

		[BindProperty(SupportsGet = true)]
		public string ContractId { get; set; }

		public CreateModel(
			IDocumentPageService pageService,
			IStoreRepositoryFactory storeFactory,
			IEnvelopeDocumentFactory envelopeDocumentFactory,
			ICertificateManagementService certificateManagementService,
			ICurrentUserContext currentUserContext) {
			_pageService = pageService;
			_storeFactory = storeFactory;
			_envelopeDocumentFactory = envelopeDocumentFactory;
			_certificateManagementService = certificateManagementService;
			_userId = currentUserContext.UserId;
		}

		public async Task<IActionResult> OnGetAsync() {
			try {
				if (string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(ContractId)) {
					if (!_certificateManagementService.HasActiveSigningCertificate()) {
						CertificateRequiredMessage = "A signing certificate is required to request signatures. Upload and activate a local PFX certificate or configure Azure Key Vault in the admin portal before creating a signature request.";
						return Page();
					}

					var envelopeId = CreateEnvelopeFromContract(ContractId);
					return RedirectToPage("/Envelopes/Create", new { id = envelopeId });
				}

				if (string.IsNullOrEmpty(Id)) {
					return NotFound();
				}

				Envelope = await _pageService.GetEnvelopeAsync(_userId, Id);
				Certificates = await _certificateManagementService.GetCertificatesAsync();
				DefaultCertificateId = _certificateManagementService.GetDefaultLocalCertificateId();

				return Page();
			} catch (UnauthorizedAccessException) {
				return Forbid();
			} catch (Exception) {
				return NotFound();
			}
		}

		private string CreateEnvelopeFromContract(string accessId) {
			byte[] octets = Convert.FromBase64String(accessId);
			var parts = Encoding.UTF8.GetString(octets).Split(':');

			if (parts.Length < 2) {
				throw new InvalidOperationException("Invalid contract access id.");
			}

			var contractId = parts[0];
			var ownerId = string.Join(":", parts.Skip(1));
			var contractStore = _storeFactory.CreateContractRepository(ownerId);
			var contract = contractStore.GetContracts(contractId).FirstOrDefault() ?? throw new InvalidOperationException("Contract not found.");
			var document = contractStore.GetDocument(contract.ContractID);

			using var stream = new MemoryStream(Convert.FromBase64String(document));
			var envelopeId = _envelopeDocumentFactory.CreateEnvelopeFromDocument(_userId, User.Identity?.Name, stream, Guid.NewGuid() + ".tx");

			if (!string.IsNullOrWhiteSpace(envelopeId) && contract.Signer != null) {
				var envelopeStore = _storeFactory.CreateEnvelopeRepository(_userId);
				var envelope = envelopeStore.GetEnvelopes(envelopeId).FirstOrDefault();

				if (envelope != null && !envelope.Signers.Any(signer =>
					string.Equals(signer.Email, contract.Signer.Email, StringComparison.OrdinalIgnoreCase))) {
					envelope.Signers.Add(new Signer {
						Id = Guid.NewGuid().ToString(),
						Name = contract.Signer.Name,
						Email = contract.Signer.Email,
						SignerStatus = SignerStatus.None
					});

					envelope.Status = EnvelopeStatus.New;
					envelopeStore.Update(envelope.EnvelopeID, envelope);
				}
			}

			return envelopeId;
		}
	}
}
