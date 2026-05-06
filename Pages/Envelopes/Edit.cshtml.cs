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
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Pages.Envelopes {
	[Authorize]
	public class EditModel : PageModel {
		private readonly IEnvelopeService _envelopeService;
		private readonly IDocumentPageService _pageService;
		private readonly string _userId;

		public Envelope Envelope { get; set; }
		public string DocumentContent { get; set; }
		public string ThumbnailSvg { get; set; }
		public string ErrorMessage { get; set; }

		public EditModel(
			IEnvelopeService envelopeService,
			IDocumentPageService pageService,
			ICurrentUserContext currentUserContext) {
			_envelopeService = envelopeService ?? throw new ArgumentNullException(nameof(envelopeService));
			_pageService = pageService ?? throw new ArgumentNullException(nameof(pageService));
			_userId = currentUserContext.UserId;
		}

		public async Task<IActionResult> OnGetAsync(string id) {
			try {
				if (string.IsNullOrEmpty(id)) {
					return NotFound();
				}

				Envelope = await _envelopeService.GetAsync(id);
				if (Envelope == null) {
					return NotFound();
				}

				// Security: verify user owns this envelope
				if (Envelope.UserID != _userId) {
					return Forbid();
				}

				// TODO: Load document content and thumbnail
				DocumentContent = null;
				ThumbnailSvg = null;

				return Page();
			} catch (Exception ex) {
				ErrorMessage = $"Error loading envelope: {ex.Message}";
				return Page();
			}
		}

		public async Task<IActionResult> OnPostSaveAsync(string id) {
			try {
				if (string.IsNullOrEmpty(id)) {
					return NotFound();
				}

				Envelope = await _envelopeService.GetAsync(id);
				if (Envelope == null) {
					return NotFound();
				}

				// Security: verify user owns this envelope
				if (Envelope.UserID != _userId) {
					return Forbid();
				}

				// Save envelope changes
				await _envelopeService.UpdateAsync(Envelope);

			return RedirectToPage("Details", new { id });
		} catch (Exception ex) {
			ErrorMessage = $"Error saving envelope: {ex.Message}";
			return Page();
		}
	}

	public async Task<IActionResult> OnGetEditPartialAsync(string id) {
		if (string.IsNullOrEmpty(id)) {
			return NotFound();
		}

		var model = await _pageService.GetEnvelopeEditModelAsync(_userId, id);

		return Partial("~/Pages/Shared/EditorPartials/_Edit.cshtml", model);
	}

	public async Task<IActionResult> OnGetSignatureBoxPartialAsync(string id) {
		if (string.IsNullOrEmpty(id)) {
			return NotFound();
		}

		var model = await _pageService.GetEnvelopeSignatureBoxModelAsync(_userId, id);

		return Partial("~/Pages/Shared/EditorPartials/_SignatureBox.cshtml", model);
	}
}
}
