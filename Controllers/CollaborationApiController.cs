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
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Controllers {
	[ApiController]
	[AllowAnonymous]
	[Route("api/collaboration")]
	public class CollaborationApiController : ControllerBase {
		private readonly ICollaborationWorkflowService _workflowService;
		private readonly string _userId;

		public CollaborationApiController(
			ICollaborationWorkflowService workflowService,
			ICurrentUserContext currentUserContext) {
			_workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
			_userId = currentUserContext.UserId;
		}

		[HttpGet("document/{id}")]
		[HttpGet("/collaboration/document/{id}")]
		public async Task<IActionResult> Document(string id) {
			try {
				return Ok(await _workflowService.GetDocumentAsync(id));
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("save-document/{id}")]
		[HttpPost("/collaboration/saveDocument/{id}")]
		public async Task<IActionResult> SaveDocument([FromBody] SaveCollaborationDocumentRequest document, string id, bool owner) {
			try {
				string host = Request.Scheme + "://" + Request.Host;
				return Ok(await _workflowService.SaveDocumentAsync(id, document.Document, owner, _userId, host));
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}
	}
}
