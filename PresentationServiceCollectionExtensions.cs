using SignFabric.ActionFilter;
using SignFabric.Application.Identity;
using SignFabric.Infrastructure.Identity;
using SignFabric.Presentation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using LiteDB.Identity.Extensions;
using LiteDB.Identity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using TXTextControl.Web;
using TXTextControl.Web.DocumentEditor.Backend;

namespace SignFabric {
	public static class PresentationServiceCollectionExtensions {
		public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration) {
			var connectionString = configuration.GetConnectionString("IdentityLiteDB");

			EnsureLiteDbDirectory(connectionString);

			services.AddAuthentication(options => {
					options.DefaultScheme = "Identity.Application";
					options.DefaultSignInScheme = "Identity.External";
				})
				.AddCookie("Identity.Application")
				.AddCookie("Identity.External")
				.AddCookie(IdentityConstants.TwoFactorUserIdScheme)
				.AddCookie(IdentityConstants.TwoFactorRememberMeScheme)
				.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options => {
					var bearer = configuration.GetSection("Authentication:Bearer");
					var localOAuth = configuration.GetSection("Authentication:LocalOAuth");
					var authority = bearer["Authority"];
					var audience = bearer["Audience"];
					var localEnabled = localOAuth.GetValue("Enabled", false);
					var localIssuer = localOAuth["Issuer"];
					var localAudience = localOAuth["Audience"];
					var localSigningKey = localOAuth["SigningKey"];

					if (!string.IsNullOrWhiteSpace(authority)) {
						options.Authority = authority;
					}

					if (!string.IsNullOrWhiteSpace(audience)) {
						options.Audience = audience;
					}

					options.RequireHttpsMetadata = bearer.GetValue("RequireHttpsMetadata", true);
					options.MapInboundClaims = false;
					options.TokenValidationParameters = new TokenValidationParameters {
						ValidateIssuer = !string.IsNullOrWhiteSpace(authority) || localEnabled,
						ValidIssuers = new[] { localIssuer }.Where(value => !string.IsNullOrWhiteSpace(value)),
						ValidateAudience = !string.IsNullOrWhiteSpace(audience) || !string.IsNullOrWhiteSpace(localAudience),
						ValidAudiences = new[] { audience, localAudience }.Where(value => !string.IsNullOrWhiteSpace(value)),
						ValidateIssuerSigningKey = localEnabled,
						IssuerSigningKey = localEnabled && !string.IsNullOrWhiteSpace(localSigningKey)
							? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(localSigningKey))
							: null
					};
				});

			var oidc = configuration.GetSection("Authentication:OpenIdConnect");
			if (oidc.GetValue("Enabled", false)) {
				services
					.AddAuthentication()
					.AddOpenIdConnect("OpenIdConnect", oidc["DisplayName"] ?? "Single Sign-On", options => {
						options.Authority = oidc["Authority"];
						options.ClientId = oidc["ClientId"];
						options.ClientSecret = oidc["ClientSecret"];
						options.CallbackPath = oidc["CallbackPath"] ?? "/signin-oidc";
						options.SignedOutCallbackPath = oidc["SignedOutCallbackPath"] ?? "/signout-callback-oidc";
						options.ResponseType = oidc["ResponseType"] ?? "code";
						options.SaveTokens = oidc.GetValue("SaveTokens", true);
						options.SignInScheme = IdentityConstants.ExternalScheme;
						options.RequireHttpsMetadata = oidc.GetValue("RequireHttpsMetadata", true);

						options.Scope.Clear();
						foreach (var scope in oidc.GetSection("Scopes").Get<string[]>() ?? new[] { "openid", "profile", "email" }) {
							if (!string.IsNullOrWhiteSpace(scope)) {
								options.Scope.Add(scope);
							}
						}

						options.TokenValidationParameters.NameClaimType = "name";
						options.TokenValidationParameters.RoleClaimType = "roles";
					});
			}

			services.AddLiteDBIdentity(connectionString)
				.AddRoles<LiteDbRole>()
				.AddDefaultTokenProviders()
				.AddDefaultUI();
			services.AddAuthorization(options => {
				options.FallbackPolicy = new AuthorizationPolicyBuilder()
					.RequireAuthenticatedUser()
					.Build();
				options.AddPolicy(ApiAuthorization.EnvelopeCreatePolicy, policy => {
					policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
					policy.RequireAuthenticatedUser();
					policy.RequireAssertion(context =>
						ApiAuthorization.HasPermission(context.User, ApiAuthorization.EnvelopeCreatePermission));
				});
				options.AddPolicy(ApiAuthorization.EnvelopeReadPolicy, policy => {
					policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
					policy.RequireAuthenticatedUser();
					policy.RequireAssertion(context =>
						ApiAuthorization.HasPermission(context.User, ApiAuthorization.EnvelopeReadPermission) ||
						ApiAuthorization.HasPermission(context.User, ApiAuthorization.EnvelopeCreatePermission));
				});
			});
			services.AddRazorPages(options => {
				options.Conventions.AllowAnonymousToFolder("/Review");
				options.Conventions.AllowAnonymousToPage("/Review/Index");
				options.Conventions.AllowAnonymousToPage("/Review/Sign");
				options.Conventions.AllowAnonymousToPage("/Review/SignLegacy");
				options.Conventions.AllowAnonymousToPage("/Review/FullySigned");
				options.Conventions.AllowAnonymousToPage("/Review/CreateAccount");
				options.Conventions.AllowAnonymousToPage("/Review/Validate");
			});
			services.AddControllersWithViews(options => {
				options.Conventions.Add(new TextControlAnonymousControllerConvention());
			});
			services.AddHostedService<DocumentEditorWorkerManager>();
			services.AddHostedService<IdentityBootstrapHostedService>();

			return services;
		}

		private static void EnsureLiteDbDirectory(string connectionString) {
			if (string.IsNullOrWhiteSpace(connectionString)) {
				throw new InvalidOperationException("The IdentityLiteDB connection string is missing.");
			}

			var builder = new DbConnectionStringBuilder {
				ConnectionString = connectionString
			};

			if (!builder.TryGetValue("Filename", out var filenameValue)) {
				throw new InvalidOperationException("The IdentityLiteDB connection string must contain a Filename value.");
			}

			var directory = Path.GetDirectoryName(Path.GetFullPath(filenameValue.ToString()));

			if (!string.IsNullOrWhiteSpace(directory)) {
				Directory.CreateDirectory(directory);
			}
		}

		public static WebApplication UsePresentationPipeline(this WebApplication app) {
			app.UseExceptionHandler(errorApp => {
				errorApp.Run(HandleExceptionAsync);
			});
			app.UseStatusCodePagesWithReExecute("/Error/{0}");

			if (!app.Environment.IsDevelopment()) {
				app.UseHsts();
			}

			app.UseHttpsRedirection();
			app.UseWebSockets();
			app.UseTXWebSocketMiddleware();
			app.UseMiddleware<OpenedMiddleware>();
			app.UseStaticFiles();
			app.UseRouting();
			app.UseAuthentication();
			app.UseAuthorization();
			app.MapControllers();
			app.MapRazorPages()
				.Add(endpointBuilder => {
					var displayName = endpointBuilder.DisplayName ?? string.Empty;
					if (displayName.Contains("/Review/", StringComparison.OrdinalIgnoreCase) ||
						displayName.Contains("Pages.Review.", StringComparison.OrdinalIgnoreCase)) {
						endpointBuilder.Metadata.Add(new AllowAnonymousAttribute());
					}
				});
			app.MapControllerRoute(
				name: "textcontrol-resources",
				pattern: "TextControl/{action}/{id?}")
				.AllowAnonymous();
			app.MapControllerRoute(
				name: "default",
				pattern: "{controller}/{action}/{id?}");

			return app;
		}

		private static async Task HandleExceptionAsync(HttpContext context) {
			var feature = context.Features.Get<IExceptionHandlerFeature>();
			var message = GetUserErrorMessage(feature?.Error);

			if (IsTextControlSigningRequest(context.Request, feature?.Error)) {
				context.Response.StatusCode = StatusCodes.Status200OK;
				context.Response.ContentType = "text/plain";
				await context.Response.WriteAsync(message);
				return;
			}

			if (WantsJsonError(context.Request)) {
				context.Response.StatusCode = StatusCodes.Status500InternalServerError;
				context.Response.ContentType = "application/json";
				await context.Response.WriteAsync(JsonSerializer.Serialize(new {
					success = false,
					error = message
				}));
				return;
			}

			context.Response.Redirect("/Error/500");
		}

		private static bool WantsJsonError(HttpRequest request) {
			if (string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)) {
				return true;
			}

			if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)) {
				return true;
			}

			var accept = request.Headers.Accept.ToString();
			return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase) &&
				!accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsTextControlSigningRequest(HttpRequest request, Exception exception) {
			var path = request.Path.Value ?? string.Empty;
			if (path.Contains("SignDocument", StringComparison.OrdinalIgnoreCase)) {
				return true;
			}

			return string.Equals(exception?.Source, "TXTextControl.Web.MVC.DocumentViewer", StringComparison.OrdinalIgnoreCase);
		}

		private static string GetUserErrorMessage(Exception exception) {
			if (string.Equals(exception?.Source, "TXTextControl.Web.MVC.DocumentViewer", StringComparison.OrdinalIgnoreCase)) {
				return "The signing session could not be completed. Please reload the signing page and try again.";
			}

			if (exception is InvalidOperationException && !string.IsNullOrWhiteSpace(exception.Message)) {
				return exception.Message;
			}

			if (exception is UnauthorizedAccessException) {
				return "You do not have access to this item.";
			}

			return "Something went wrong while processing your request. Please try again.";
		}
	}
}
