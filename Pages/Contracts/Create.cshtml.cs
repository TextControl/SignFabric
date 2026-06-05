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
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SignFabric.Pages.Contracts {
	[Authorize(Roles = SignFabric.Application.Identity.AppRoles.EnvelopeCreators)]
	public class CreateModel : PageModel {
		private readonly IDocumentProcessingService _documentService;
		private readonly IUploadPolicy _uploadPolicy;
		private readonly string _userId;

		[BindProperty]
		public IFormFile UploadedFile { get; set; }

		public string ErrorMessage { get; set; }
		public string SuccessMessage { get; set; }
		public string AcceptedFileTypes => _uploadPolicy.AcceptAttribute;

		public CreateModel(
			IDocumentProcessingService documentService,
			IUploadPolicy uploadPolicy,
			ICurrentUserContext currentUserContext) {
			_documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
			_uploadPolicy = uploadPolicy ?? throw new ArgumentNullException(nameof(uploadPolicy));
			_userId = currentUserContext.UserId;
		}

		public void OnGet() { }

		public async Task<IActionResult> OnPostAsync() {
			try {
				if (UploadedFile == null || UploadedFile.Length == 0) {
					ErrorMessage = "Please select a file to upload.";
					return Page();
				}

				if (!_uploadPolicy.IsAllowed(UploadedFile.FileName, UploadedFile.Length, out var uploadError)) {
					ErrorMessage = uploadError;
					return Page();
				}

				using (var ms = new MemoryStream()) {
					await UploadedFile.CopyToAsync(ms);
					
					var (template, thumbnail) = await _documentService.ProcessNewTemplateAsync(
						ms,
						UploadedFile.FileName,
						_userId);

					SuccessMessage = $"Contract created successfully!";
					return RedirectToPage("Index");
				}
			} catch (Exception ex) {
				ErrorMessage = $"Error creating contract: {ex.Message}";
				return Page();
			}
		}
	}
}
