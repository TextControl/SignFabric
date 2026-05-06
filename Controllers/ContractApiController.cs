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
	public class ContractApiController : ControllerBase {
		private readonly IContractWorkflowService _workflowService;
		private readonly IEditableDocumentService _editableDocumentService;
		private readonly IUploadPolicy _uploadPolicy;
		private readonly UserManager<LiteDB.Identity.Models.LiteDbUser> _userManager;
		private readonly string _userId;

		public ContractApiController(
			IContractWorkflowService workflowService,
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
		[HttpGet("/contract/document/{id}")]
		public async Task<IActionResult> GetDocument(string id) {
			try {
				return Ok(await _editableDocumentService.GetEditableDocumentAsync(_userId, "contract", id));
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("save-document/{id}")]
		[HttpPost("/contract/saveDocument/{id}")]
		public async Task<IActionResult> SaveDocument([FromBody] SaveDocumentRequest document, string id) {
			try {
				await _editableDocumentService.SaveDocumentAsync(_userId, "contract", id, document.Document);
				return Ok(new { success = true, message = "Contract saved" });
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("update-recipient/{id}")]
		[HttpPost("/contract/updaterecipient/{id}")]
		public async Task<IActionResult> UpdateRecipient([FromBody] Signer signer, string id) {
			try {
				return Ok(await _workflowService.AddRecipientAsync(_userId, id, signer));
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("submit/{id}")]
		[HttpPost("/contract/submit/{id}")]
		public async Task<IActionResult> Submit(string id) {
			try {
				var host = Request.Scheme + "://" + Request.Host;
				return Ok(await _workflowService.SubmitAsync(_userId, id, host));
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("/contract/new")]
		public async Task<IActionResult> New() {
			try {
				NewContractModel contract = null;
				foreach (var file in Request.Form.Files) {
					if (!_uploadPolicy.IsAllowed(file.FileName, file.Length, out var uploadError)) {
						return BadRequest(uploadError);
					}

					using (var ms = new MemoryStream()) {
						file.CopyTo(ms);
						contract = await _workflowService.CreateAsync(_userId, _userManager.GetUserName(User), ms, file.FileName);
					}
				}
				return Ok(contract);
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpGet("/contracts/{id}/download")]
		[HttpGet("/contract/download/{id}")]
		public async Task<IActionResult> Download(string id) {
			try {
				var download = await _workflowService.DownloadAsync(_userId, id);
				return File(download.Document, "application/octet-stream", download.FileName);
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}
	}
}
