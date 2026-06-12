using SignFabric.Application.Services;
using SignFabric.Application.Abstractions;
using SignFabric.Application.Identity;
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
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Controllers {
	[Authorize(Roles = AppRoles.EnvelopeCreators)]
	[ApiController]
	[Route("api/[controller]")]
	public class TemplateApiController : ControllerBase {
		private readonly ITemplateWorkflowService _workflowService;
		private readonly IEditableDocumentService _editableDocumentService;
		private readonly IDocumentPageService _pageService;
		private readonly IUploadPolicy _uploadPolicy;
		private readonly UserManager<LiteDB.Identity.Models.LiteDbUser> _userManager;
		private readonly string _userId;

		public TemplateApiController(
			ITemplateWorkflowService workflowService,
			IEditableDocumentService editableDocumentService,
			IDocumentPageService pageService,
			IUploadPolicy uploadPolicy,
			ICurrentUserContext currentUserContext,
			UserManager<LiteDB.Identity.Models.LiteDbUser> userManager) {
			_workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
			_editableDocumentService = editableDocumentService ?? throw new ArgumentNullException(nameof(editableDocumentService));
			_pageService = pageService ?? throw new ArgumentNullException(nameof(pageService));
			_uploadPolicy = uploadPolicy ?? throw new ArgumentNullException(nameof(uploadPolicy));
			_userManager = userManager;
			_userId = currentUserContext.UserId;
		}

		[HttpGet("document/{id}")]
		[HttpGet("/template/document/{id}")]
		public async Task<IActionResult> GetDocument(string id) {
			try {
				return Ok(await _editableDocumentService.GetEditableDocumentAsync(_userId, "template", id));
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpGet("summary/{id}")]
		[HttpGet("/template/summary/{id}")]
		public async Task<IActionResult> GetSummary(string id) {
			try {
				var model = await _pageService.GetTemplateDetailsAsync(_userId, id);
				return Ok(new {
					templateId = model.Template.TemplateID,
					name = model.Template.Name,
					thumbnailSvg = model.ThumbnailSvg
				});
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("save-document/{id}")]
		[HttpPost("/template/saveDocument/{id}")]
		public async Task<IActionResult> SaveDocument([FromBody] SaveDocumentRequest document, string id) {
			try {
				await _editableDocumentService.SaveDocumentAsync(_userId, "template", id, document.Document);
				return Ok(new { success = true, message = "Template saved" });
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("createnew")]
		[HttpPost("/template/createnew")]
		public async Task<IActionResult> CreateNew([FromBody] CreateTemplateRequest request) {
			try {
				return Ok(await _workflowService.CreateBlankAsync(_userId, request?.Name));
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("rename/{id}")]
		[HttpPost("/template/rename/{id}")]
		public async Task<IActionResult> Rename(string id, [FromBody] RenameTemplateRequest request) {
			try {
				await _workflowService.RenameAsync(_userId, id, request?.Name);
				return Ok(new { success = true });
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("/template/new")]
		public async Task<IActionResult> New() {
			try {
				NewTemplateModel template = null;
				foreach (var file in Request.Form.Files) {
					if (!_uploadPolicy.IsAllowed(file.FileName, file.Length, out var uploadError)) {
						return BadRequest(uploadError);
					}

					using (var ms = new MemoryStream()) {
						file.CopyTo(ms);
						template = await _workflowService.CreateAsync(_userId, ms, file.FileName);
						if (template == null) {
							return BadRequest(new {
								error = "The selected file could not be converted to a supported TX document. Please upload a valid PDF, DOCX, RTF, DOC, HTML, or TX document.",
								success = false
							});
						}
					}
				}
				return Ok(template);
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("getfields/{id}")]
		[HttpPost("/template/getfields/{id}")]
		public async Task<IActionResult> GetFields(string id) {
			try {
				return Ok(await _workflowService.GetFieldsAsync(_userId, id));
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("instance/{id}")]
		[HttpPost("/template/instance/{id}")]
		public async Task<IActionResult> Instance(string id) {
			try {
				var fields = new Dictionary<string, string>();
				foreach (string key in Request.Form.Keys) {
					fields[key] = Request.Form[key];
				}
				string documentId = await _workflowService.CreateEnvelopeFromTemplateAsync(_userId, _userManager.GetUserName(User), id, fields);
				return Redirect("/envelopes/create/" + documentId);
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}

		[HttpPost("contract/{id}")]
		[HttpPost("/template/contract/{id}")]
		public async Task<IActionResult> Contract(string id) {
			try {
				var fields = new Dictionary<string, string>();
				foreach (string key in Request.Form.Keys) {
					fields[key] = Request.Form[key];
				}
				string contractId = await _workflowService.CreateContractFromTemplateAsync(_userId, _userManager.GetUserName(User), id, fields);
				return Redirect("/contracts/create/" + contractId);
			} catch (Exception ex) {
				return BadRequest(new { error = ex.Message, success = false });
			}
		}
	}
}
