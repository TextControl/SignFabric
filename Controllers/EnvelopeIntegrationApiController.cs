using SignFabric.Application.Abstractions;
using SignFabric.Application.Identity;
using SignFabric.Application.Services;
using SignFabric.Domain;
using LiteDB.Identity.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Controllers {
	[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
	[ApiController]
	[Route("api/v1/envelopes")]
	public class EnvelopeIntegrationApiController : ControllerBase {
		private readonly IEnvelopeWorkflowService _workflowService;
		private readonly IStoreRepositoryFactory _storeFactory;
		private readonly IUploadPolicy _uploadPolicy;
		private readonly IFieldExtractionService _fieldExtractionService;
		private readonly UserManager<LiteDbUser> _userManager;
		private readonly string _userId;
		private readonly string _userName;

		public EnvelopeIntegrationApiController(
			IEnvelopeWorkflowService workflowService,
			IStoreRepositoryFactory storeFactory,
			IUploadPolicy uploadPolicy,
			IFieldExtractionService fieldExtractionService,
			ICurrentUserContext currentUserContext,
			UserManager<LiteDbUser> userManager) {
			_workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
			_storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
			_uploadPolicy = uploadPolicy ?? throw new ArgumentNullException(nameof(uploadPolicy));
			_fieldExtractionService = fieldExtractionService ?? throw new ArgumentNullException(nameof(fieldExtractionService));
			_userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
			_userId = currentUserContext.UserId;
			_userName = currentUserContext.UserName;
		}

		[HttpPost]
		[Authorize(Policy = ApiAuthorization.EnvelopeCreatePolicy)]
		public async Task<IActionResult> Create([FromBody] CreateEnvelopeApiRequest request) {
			var validationErrors = ValidateCreateRequest(request).ToList();
			if (validationErrors.Any()) {
				return BadRequest(ApiError(validationErrors));
			}

			byte[] documentBytes;
			try {
				documentBytes = Convert.FromBase64String(NormalizeBase64(request.DocumentBase64));
			}
			catch (FormatException) {
				return BadRequest(ApiError("DocumentBase64 must contain a valid Base64 encoded document."));
			}

			if (!_uploadPolicy.IsAllowed(request.FileName, documentBytes.LongLength, out var uploadError)) {
				return BadRequest(ApiError(uploadError));
			}

			var signers = request.Signers
				.Select(signer => new Signer {
					Id = signer.Id.Trim(),
					Name = signer.Name.Trim(),
					Email = signer.Email.Trim()
				})
				.ToList();

			var store = _storeFactory.CreateEnvelopeRepository(_userId);
			string envelopeId = null;

			try {
				using var stream = new MemoryStream(documentBytes);
				envelopeId = await _workflowService.CreateAsync(
					_userId,
					_userManager.GetUserName(User) ?? _userName,
					stream,
					request.FileName,
					request.SigningCertificateId);

				var envelope = store.GetEnvelopes(envelopeId).FirstOrDefault()
					?? throw new InvalidOperationException("Envelope was created but could not be loaded.");

				envelope.Signers = signers;
				envelope.Status = EnvelopeStatus.New;

				var document = store.GetDocument(envelope.EnvelopeID);
				envelope.ContainsSignatureBoxes = await _fieldExtractionService.ContainsSignatureBoxesAsync(
					document,
					envelope.Signers.Where(signer => signer.Role == RecipientRole.Signer).ToList());

				if (!envelope.ContainsSignatureBoxes) {
					envelope.Status = EnvelopeStatus.Incomplete;
					envelope.FaultMessage = "The document does not contain a matching signature field for every signer. Each signer id must have a signature field named txsign_{signerId}.";
					store.Update(envelope.EnvelopeID, envelope);

					return BadRequest(new {
						success = false,
						envelopeId = envelope.EnvelopeID,
						errors = new[] { envelope.FaultMessage }
					});
				}

				store.Update(envelope.EnvelopeID, envelope);

				if (request.SendImmediately.GetValueOrDefault(true)) {
					var host = $"{Request.Scheme}://{Request.Host}";
					envelope = await _workflowService.SubmitAsync(_userId, envelope.EnvelopeID, host);
				}

				var response = ToEnvelopeResponse(envelope);
				return CreatedAtAction(nameof(Get), new { id = envelope.EnvelopeID }, response);
			}
			catch (Exception ex) {
				if (!string.IsNullOrWhiteSpace(envelopeId)) {
					var envelope = store.GetEnvelopes(envelopeId).FirstOrDefault();
					if (envelope != null) {
						envelope.Status = EnvelopeStatus.Faulted;
						envelope.FaultMessage = ex.Message;
						store.Update(envelope.EnvelopeID, envelope);
					}
				}

				return BadRequest(ApiError(ex.Message));
			}
		}

		[HttpGet]
		[Authorize(Policy = ApiAuthorization.EnvelopeReadPolicy)]
		public IActionResult List() {
			var envelopes = _storeFactory
				.CreateEnvelopeRepository(_userId)
				.GetEnvelopes()
				.OrderByDescending(envelope => envelope.Created)
				.Select(ToEnvelopeSummary)
				.ToList();

			return Ok(new {
				success = true,
				count = envelopes.Count,
				envelopes
			});
		}

		[HttpGet("{id}")]
		[Authorize(Policy = ApiAuthorization.EnvelopeReadPolicy)]
		public IActionResult Get(string id) {
			var envelope = _storeFactory
				.CreateEnvelopeRepository(_userId)
				.GetEnvelopes(id)
				.FirstOrDefault();

			if (envelope == null) {
				return NotFound(ApiError("Envelope not found."));
			}

			return Ok(ToEnvelopeResponse(envelope));
		}

		[HttpGet("{id}/status")]
		[Authorize(Policy = ApiAuthorization.EnvelopeReadPolicy)]
		public IActionResult GetStatus(string id) {
			var envelope = _storeFactory
				.CreateEnvelopeRepository(_userId)
				.GetEnvelopes(id)
				.FirstOrDefault();

			if (envelope == null) {
				return NotFound(ApiError("Envelope not found."));
			}

			return Ok(new {
				success = true,
				envelopeId = envelope.EnvelopeID,
				status = envelope.Status.ToString(),
				faultMessage = envelope.FaultMessage,
				signers = envelope.Signers.Select(ToSignerResponse).ToList()
			});
		}

		private static IEnumerable<string> ValidateCreateRequest(CreateEnvelopeApiRequest request) {
			if (request == null) {
				yield return "Request body is required.";
				yield break;
			}

			if (string.IsNullOrWhiteSpace(request.FileName)) {
				yield return "FileName is required.";
			}

			if (string.IsNullOrWhiteSpace(request.DocumentBase64)) {
				yield return "DocumentBase64 is required.";
			}

			if (request.Signers == null || request.Signers.Count == 0) {
				yield return "At least one signer is required.";
				yield break;
			}

			var signerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var signerEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var emailAddress = new EmailAddressAttribute();

			for (var i = 0; i < request.Signers.Count; i++) {
				var signer = request.Signers[i];
				var label = $"Signers[{i}]";

				if (signer == null) {
					yield return $"{label} is required.";
					continue;
				}

				if (string.IsNullOrWhiteSpace(signer.Id)) {
					yield return $"{label}.Id is required and must match the TX signature field suffix in the document.";
				}
				else if (!signerIds.Add(signer.Id.Trim())) {
					yield return $"{label}.Id must be unique.";
				}

				if (string.IsNullOrWhiteSpace(signer.Name)) {
					yield return $"{label}.Name is required.";
				}

				if (string.IsNullOrWhiteSpace(signer.Email) || !emailAddress.IsValid(signer.Email)) {
					yield return $"{label}.Email must be a valid e-mail address.";
				}
				else if (!signerEmails.Add(signer.Email.Trim())) {
					yield return $"{label}.Email must be unique.";
				}
			}
		}

		private static string NormalizeBase64(string value) {
			var trimmed = value.Trim();
			var commaIndex = trimmed.IndexOf(',');
			return trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0
				? trimmed[(commaIndex + 1)..]
				: trimmed;
		}

		private static object ApiError(params string[] errors) =>
			ApiError((IEnumerable<string>)errors);

		private static object ApiError(IEnumerable<string> errors) =>
			new {
				success = false,
				errors = errors.Where(error => !string.IsNullOrWhiteSpace(error)).ToArray()
			};

		private static object ToEnvelopeSummary(Envelope envelope) =>
			new {
				envelopeId = envelope.EnvelopeID,
				name = envelope.Name,
				status = envelope.Status.ToString(),
				created = envelope.Created,
				sent = envelope.Sent == default ? (DateTime?)null : envelope.Sent,
				signerCount = envelope.Signers?.Count ?? 0,
				faultMessage = envelope.FaultMessage
			};

		private static object ToEnvelopeResponse(Envelope envelope) =>
			new {
				success = true,
				envelopeId = envelope.EnvelopeID,
				name = envelope.Name,
				status = envelope.Status.ToString(),
				created = envelope.Created,
				sent = envelope.Sent == default ? (DateTime?)null : envelope.Sent,
				containsSignatureBoxes = envelope.ContainsSignatureBoxes,
				signingCertificateId = envelope.SigningCertificateId,
				faultMessage = envelope.FaultMessage,
				signers = envelope.Signers.Select(ToSignerResponse).ToList()
			};

		private static object ToSignerResponse(Signer signer) =>
			new {
				id = signer.Id,
				name = signer.Name,
				email = signer.Email,
				status = signer.SignerStatus.ToString(),
				statusHistory = signer.StatusChanged
					.Select(status => new {
						status = status.SignerStatus.ToString(),
						timeStamp = status.TimeStamp
					})
					.ToList()
			};
	}

	public class CreateEnvelopeApiRequest {
		public string FileName { get; set; }
		public string DocumentBase64 { get; set; }
		public List<CreateEnvelopeSignerApiRequest> Signers { get; set; } = new();
		public string SigningCertificateId { get; set; }
		public bool? SendImmediately { get; set; } = true;
	}

	public class CreateEnvelopeSignerApiRequest {
		public string Id { get; set; }
		public string Name { get; set; }
		public string Email { get; set; }
	}
}
