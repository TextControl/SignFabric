using SignFabric.Application.Services;
using SignFabric.Application.ContractManagement;
using SignFabric.Application.Envelopes;
using SignFabric.Application.Signing;
using SignFabric.Application.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TXTextControl.Web.MVC.DocumentViewer.Models;

namespace SignFabric.Controllers {
	/// <summary>
	/// API Controller for Review/Signing endpoints.
	/// </summary>
	[ApiController]
	[AllowAnonymous]
	[Route("api/[controller]")]
	public class ReviewController : ControllerBase {
		private readonly ISigningWorkflowService _signingWorkflowService;

		public ReviewController(ISigningWorkflowService signingWorkflowService) {
			_signingWorkflowService = signingWorkflowService ?? throw new ArgumentNullException(nameof(signingWorkflowService));
		}

		/// <summary>POST /review/SignDocumentFinal - Complete signature process (called by TXTextControl DocumentViewer)</summary>
		[HttpPost("/review/SignDocumentFinal")]
		public async Task<IActionResult> SignDocumentFinal([FromBody] SignatureData data, string userID, string envelopeId, string signerId) {
			try {
				if (data?.SignedDocument == null) {
					return BadRequest(new {
						error = "The signed document data was not received. Please reload the signing page and try again.",
						success = false
					});
				}

				await _signingWorkflowService.CompleteDocumentViewerSigningAsync(
					data,
					userID,
					envelopeId,
					signerId,
					Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown");

				return Ok(true);
			} catch (Exception ex) {
				return Ok(string.IsNullOrWhiteSpace(ex.Message)
					? "The document could not be finalized. Please contact the sender."
					: ex.Message);
			}
		}
	}
}
