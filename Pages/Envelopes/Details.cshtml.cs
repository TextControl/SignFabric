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
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Pages.Envelopes {
	[Authorize]
	public class DetailsModel : PageModel {
		private readonly IDocumentPageService _pageService;
		private readonly ISigningWorkflowService _signingWorkflowService;
		private readonly string _userId;

		public Envelope Envelope { get; set; }
		public string ThumbnailSvg { get; set; }
		private Dictionary<string, string> _signatureImages = new();

		public DetailsModel(
			IDocumentPageService pageService,
			ISigningWorkflowService signingWorkflowService,
			ICurrentUserContext currentUserContext) {
			_pageService = pageService;
			_signingWorkflowService = signingWorkflowService;
			_userId = currentUserContext.UserId;
		}

		public async Task<IActionResult> OnGetAsync(string id) {
			try {
				if (string.IsNullOrEmpty(id)) {
					return NotFound();
				}

				var model = await _pageService.GetEnvelopeDetailsAsync(_userId, id);
				Envelope = model.Envelope;

				if (Envelope.Signers.All(signer => signer.SignerStatus == SignerStatus.Signed) &&
					Envelope.Status != EnvelopeStatus.Signed &&
					Envelope.Status != EnvelopeStatus.Faulted) {
					try {
						await _signingWorkflowService.GenerateFinalDocumentAsync(id);
					} catch (InvalidOperationException) {
						// The workflow stores the fault on the envelope; reload it so the page can explain the problem.
					}
					model = await _pageService.GetEnvelopeDetailsAsync(_userId, id);
					Envelope = model.Envelope;
				}

				ThumbnailSvg = model.ThumbnailSvg;
				_signatureImages = model.SignatureImages;

				return Page();
			} catch (UnauthorizedAccessException) {
				return Forbid();
			} catch (InvalidOperationException) {
				return NotFound();
			} catch (Exception) {
				return NotFound();
			}
		}

		public async Task<IActionResult> OnGetDownloadAsync(string id) {
			try {
				if (string.IsNullOrEmpty(id)) {
					return NotFound();
				}

				var download = await DownloadEnvelopeWithFinalizationRetryAsync(id);
				return File(download.Document, "application/pdf", $"{download.FileName}.pdf");
			} catch (UnauthorizedAccessException) {
				return Forbid();
			} catch (Exception ex) {
				return BadRequest($"Error downloading document: {ex.Message}");
			}
		}

		public string GetSignatureImage(string signerId) {
			return _signatureImages.ContainsKey(signerId) ? _signatureImages[signerId] : "";
		}

		private async Task<(byte[] Document, string FileName)> DownloadEnvelopeWithFinalizationRetryAsync(string id) {
			try {
				return await _pageService.DownloadEnvelopeAsync(_userId, id);
			} catch (InvalidOperationException) {
				await _signingWorkflowService.GenerateFinalDocumentAsync(id);
				return await _pageService.DownloadEnvelopeAsync(_userId, id);
			}
		}
	}
}
