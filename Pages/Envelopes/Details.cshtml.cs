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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Pages.Envelopes {
	[AllowAnonymous]
	public class DetailsModel : PageModel {
		private readonly IDocumentPageService _pageService;
		private readonly ISigningWorkflowService _signingWorkflowService;
		private readonly ISignerDocumentService _signerDocumentService;
		private readonly string _userId;

		public Envelope Envelope { get; set; }
		public string ThumbnailSvg { get; set; }
		public bool IsSignerAccount { get; set; }
		public bool IsApprovalView { get; set; }
		public bool IsRecipientStatusView { get; set; }
		public bool ApprovalNotActiveYet { get; set; }
		public string ApprovalAccessId { get; set; }
		public Signer ApprovalRecipient { get; set; }
		public Signer StatusRecipient { get; set; }
		public string ApprovalErrorMessage { get; set; }
		private Dictionary<string, string> _signatureImages = new();

		public DetailsModel(
			IDocumentPageService pageService,
			ISigningWorkflowService signingWorkflowService,
			ISignerDocumentService signerDocumentService,
			ICurrentUserContext currentUserContext) {
			_pageService = pageService;
			_signingWorkflowService = signingWorkflowService;
			_signerDocumentService = signerDocumentService;
			_userId = currentUserContext.UserId;
		}

		public async Task<IActionResult> OnGetAsync(string id, string approvalId, string accessId) {
			try {
				if (string.IsNullOrEmpty(id)) {
					return NotFound();
				}

				if (!string.IsNullOrWhiteSpace(approvalId)) {
					return await LoadApprovalViewAsync(approvalId);
				}

				if (!string.IsNullOrWhiteSpace(accessId)) {
					return await LoadRecipientStatusViewAsync(accessId);
				}

				if (User?.Identity?.IsAuthenticated != true) {
					return Challenge();
				}

				IsSignerAccount = User.IsInRole(AppRoles.Signer);
				var model = IsSignerAccount
					? await _signerDocumentService.GetSignedDocumentDetailsAsync(GetSignerEmail(), id)
					: await _pageService.GetEnvelopeDetailsAsync(_userId, id);
				Envelope = model.Envelope;

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

				IsSignerAccount = User.IsInRole(AppRoles.Signer);
				var download = IsSignerAccount
					? await _signerDocumentService.DownloadSignedDocumentAsync(GetSignerEmail(), id)
					: await DownloadEnvelopeWithFinalizationRetryAsync(id);
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

		public async Task<IActionResult> OnPostApproveAsync(string id, string approvalId, string comment) {
			return await CompleteApprovalAsync(id, approvalId, approved: true, comment);
		}

		public async Task<IActionResult> OnPostDeclineAsync(string id, string approvalId, string comment) {
			return await CompleteApprovalAsync(id, approvalId, approved: false, comment);
		}

		private async Task<IActionResult> CompleteApprovalAsync(string id, string approvalId, bool approved, string comment) {
			try {
				await _signingWorkflowService.CompleteApprovalAsync(
					approvalId,
					approved,
					comment,
					Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
					Request.Headers.UserAgent.ToString(),
					Request.Scheme + "://" + Request.Host);

				return RedirectToPage("/Envelopes/Details", new { id, approvalId });
			}
			catch (Exception ex) {
				await LoadApprovalViewAsync(approvalId);
				ApprovalErrorMessage = ex.Message;
				return Page();
			}
		}

		private async Task<IActionResult> LoadApprovalViewAsync(string approvalId) {
			var preparation = await _signingWorkflowService.PrepareExternalApprovalAsync(approvalId);
			var model = await _pageService.GetEnvelopeDetailsAsync(preparation.Envelope.UserID, preparation.Envelope.EnvelopeID);

			IsApprovalView = true;
			ApprovalAccessId = approvalId;
			ApprovalRecipient = preparation.Approver;
			ApprovalNotActiveYet = preparation.NotActiveYet;
			Envelope = model.Envelope;
			ThumbnailSvg = model.ThumbnailSvg;
			_signatureImages = model.SignatureImages;

			return Page();
		}

		private async Task<IActionResult> LoadRecipientStatusViewAsync(string accessId) {
			var info = await _signingWorkflowService.GetSigningThanksAsync(accessId);
			var model = await _pageService.GetEnvelopeDetailsAsync(info.Envelope.UserID, info.Envelope.EnvelopeID);

			IsRecipientStatusView = true;
			StatusRecipient = info.Signer;
			Envelope = model.Envelope;
			ThumbnailSvg = model.ThumbnailSvg;
			_signatureImages = model.SignatureImages;

			return Page();
		}

		private async Task<(byte[] Document, string FileName)> DownloadEnvelopeWithFinalizationRetryAsync(string id) {
			try {
				return await _pageService.DownloadEnvelopeAsync(_userId, id);
			} catch (InvalidOperationException) {
				await _signingWorkflowService.GenerateFinalDocumentAsync(id);
				return await _pageService.DownloadEnvelopeAsync(_userId, id);
			}
		}

		private string GetSignerEmail() =>
			User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name;
	}
}
