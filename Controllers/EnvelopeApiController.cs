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
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Controllers {
	[Authorize]
	[ApiController]
	[Route("api/[controller]")]
	public class EnvelopeController : ControllerBase {
		private readonly IEnvelopeWorkflowService _workflowService;
		private readonly IEditableDocumentService _editableDocumentService;
		private readonly IUploadPolicy _uploadPolicy;
		private readonly UserManager<LiteDB.Identity.Models.LiteDbUser> _userManager;
		private readonly string _userId;

		public EnvelopeController(
			IEnvelopeWorkflowService workflowService,
			IEditableDocumentService editableDocumentService,
			IUploadPolicy uploadPolicy,
			ICurrentUserContext currentUserContext,
			UserManager<LiteDB.Identity.Models.LiteDbUser> userManager) {
			_workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
			_editableDocumentService = editableDocumentService ?? throw new ArgumentNullException(nameof(editableDocumentService));
			_uploadPolicy = uploadPolicy ?? throw new ArgumentNullException(nameof(uploadPolicy));
			_userManager = userManager;
			_userId = currentUserContext.UserId;
		}

		[HttpGet("document/{id}")]
		[HttpGet("/envelope/document/{id}")]
		public async Task<IActionResult> GetDocument(string id) {
			try {
				return Ok(await _editableDocumentService.GetEditableDocumentAsync(_userId, "envelope", id));
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("save-document/{id}")]
		[HttpPost("/envelope/saveDocument/{id}")]
		public async Task<IActionResult> SaveDocument([FromBody] SaveDocumentRequest document, string id) {
			try {
				await _editableDocumentService.SaveDocumentAsync(_userId, "envelope", id, document.Document);
				return Ok(new { success = true, message = "Document saved" });
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("update-recipient/{id}")]
		[HttpPost("/envelope/updaterecipient/{id}")]
		public async Task<IActionResult> UpdateRecipient([FromBody] Signer signer, string id) {
			try {
				return Ok(await _workflowService.AddRecipientAsync(_userId, id, signer));
			} catch (InvalidOperationException ex) {
				return BadRequest(ex.Message);
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpGet("receive-recipients/{id}")]
		[HttpGet("/envelope/receiverecipients/{id}")]
		public async Task<IActionResult> ReceiveRecipients(string id) {
			try {
				return Ok(await _workflowService.GetRecipientsAsync(_userId, id));
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("remove-recipient/{id}")]
		[HttpPost("/envelope/removerecipient/{id}")]
		public async Task<IActionResult> RemoveRecipient([FromBody] Signer signer, string id) {
			try {
				return Ok(await _workflowService.RemoveRecipientAsync(_userId, id, signer));
			} catch (InvalidOperationException ex) {
				return BadRequest(ex.Message);
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("submit/{id}")]
		[HttpPost("/envelope/submit/{id}")]
		public async Task<IActionResult> Submit(string id) {
			try {
				var host = HttpContext.Request.Scheme + "://" + HttpContext.Request.Host;
				var envelope = await _workflowService.SubmitAsync(_userId, id, host);
				return Ok(new { success = true, envelope = envelope, message = "Envelope sent to signers" });
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("/envelope/new")]
		public async Task<IActionResult> New() {
			try {
				string envelopeId = "";

				foreach (var file in Request.Form.Files) {
					if (!_uploadPolicy.IsAllowed(file.FileName, file.Length, out var uploadError)) {
						return BadRequest(uploadError);
					}

					using (var ms = new MemoryStream()) {
						file.CopyTo(ms);
						envelopeId = await _workflowService.CreateAsync(
							_userId,
							_userManager.GetUserName(User),
							ms,
							file.FileName);
					}
				}

				return Ok(envelopeId);
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

	}
}
