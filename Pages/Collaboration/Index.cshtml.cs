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

namespace SignFabric.Pages.Collaboration {
	[AllowAnonymous]
	public class IndexModel : PageModel {
		public string DocumentId { get; set; }
		public Contract Contract { get; set; }
		public string EditorUser { get; set; }
		public bool Owner { get; set; }
		public bool IsUnavailable { get; set; }
		public bool HasError { get; set; }

		private readonly string _userId;
		private readonly ICollaborationWorkflowService _workflowService;

		public IndexModel(
			ICurrentUserContext currentUserContext,
			ICollaborationWorkflowService workflowService) {
			_userId = currentUserContext.UserId;
			_workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
		}

		public async Task<IActionResult> OnGetAsync(string id) {
			try {
				if (string.IsNullOrEmpty(id)) {
					HasError = true;
					return Page();
				}

				var review = await _workflowService.GetContractReviewAsync(id, _userId, User.Identity?.Name);
				DocumentId = review.AccessId;
				Contract = review.Contract;
				Owner = review.Owner;
				EditorUser = review.EditorUser;
				IsUnavailable = review.IsUnavailable;

				return Page();
			} catch {
				HasError = true;
				return Page();
			}
		}
	}
}
