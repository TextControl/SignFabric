using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SignFabric.Application.Services;
using SignFabric.Domain;
using System;
using System.Threading.Tasks;

namespace SignFabric.Pages.Review {
	[AllowAnonymous]
	public class ApproveModel : PageModel {
		private readonly ISigningWorkflowService _signingWorkflowService;

		public string AccessId { get; set; }
		public Envelope Envelope { get; set; }
		public Signer Approver { get; set; }
		public bool AlreadyCompleted { get; set; }
		public bool NotActiveYet { get; set; }
		public string ErrorMessage { get; set; }

		public ApproveModel(ISigningWorkflowService signingWorkflowService) {
			_signingWorkflowService = signingWorkflowService ?? throw new ArgumentNullException(nameof(signingWorkflowService));
		}

		public async Task<IActionResult> OnGetAsync(string id) {
			try {
				await BindAsync(id);
				return RedirectToPage("/Envelopes/Details", new { id = Envelope.EnvelopeID, approvalId = id });
			}
			catch {
				return RedirectToPage("Index", new { error = true });
			}
		}

		public async Task<IActionResult> OnPostApproveAsync(string id, string comment) {
			return await CompleteAsync(id, approved: true, comment);
		}

		public async Task<IActionResult> OnPostDeclineAsync(string id, string comment) {
			return await CompleteAsync(id, approved: false, comment);
		}

		private async Task<IActionResult> CompleteAsync(string id, bool approved, string comment) {
			try {
				await _signingWorkflowService.CompleteApprovalAsync(
					id,
					approved,
					comment,
					Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
					Request.Headers.UserAgent.ToString(),
					Request.Scheme + "://" + Request.Host);

				await BindAsync(id);
				AlreadyCompleted = true;
				return Page();
			}
			catch (Exception ex) {
				await BindAsync(id);
				ErrorMessage = ex.Message;
				return Page();
			}
		}

		private async Task BindAsync(string id) {
			var preparation = await _signingWorkflowService.PrepareExternalApprovalAsync(id);
			AccessId = preparation.AccessId;
			Envelope = preparation.Envelope;
			Approver = preparation.Approver;
			AlreadyCompleted = preparation.AlreadyCompleted;
			NotActiveYet = preparation.NotActiveYet;
		}
	}
}
