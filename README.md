# ✍️ Text Control SignFabric

SignFabric is an ASP.NET Core Razor Pages application for preparing, sending, reviewing, and validating documents that require electronic signatures. It uses TX Text Control components for document editing, form fields, signing, PDF generation, and validation workflows.

## ✨ Features

- Create signature envelopes from uploaded documents.
- Prepare documents with recipients, signature boxes, and form fields.
- Send signing requests with e-mail notifications and tracking.
- Review and sign documents without requiring recipients to log in.
- Validate signed documents.
- Create, rename, and use reusable document templates.
- Manage contracts and collaborative review flows.
- Admin user management with roles, status, deletion, and e-mail based two-factor authentication.
- Admin certificate management with encrypted PFX passwords and Azure Key Vault configuration.
- Admin SMTP and HTML e-mail template management.
- Admin authentication management for OpenID Connect, external bearer tokens, and local OAuth API clients.
- Versioned Web API for creating envelopes and retrieving envelope status.
- Per-user data folders for envelopes, contracts, templates, and related storage.
- Light and dark mode UI.

## 🧰 Technology Stack

- .NET 10
- ASP.NET Core Razor Pages and MVC controllers
- ASP.NET Core Identity UI
- LiteDB and LiteDB.Identity
- TX Text Control Web, Document Editor Backend, Document Viewer, and Core SDK
- Azure Key Vault certificate integration
- Bootstrap 5 and Bootstrap Icons
- BuildBundlerMinifier for CSS and JavaScript bundling

## 🗂️ Project Structure

```text
ActionFilter/       Request middleware, including signing/open tracking
Application/        Application contracts, workflows, identity abstractions
Areas/Identity/     Customized Identity pages
Controllers/        API endpoints used by the frontend flows
Domain/             Domain models
Infrastructure/     Storage, e-mail, certificates, identity, logging
Pages/              Razor Pages UI
Presentation/       View models and presentation helpers
wwwroot/            Static assets, CSS, JavaScript, Bootstrap
App_Data/           E-mail templates and application data
Data/               LiteDB databases, audit logs, per-user data
```

## ✅ Prerequisites

- Visual Studio with ASP.NET and web development workload
- .NET 10 SDK
- TX Text Control runtime/components required by the referenced TX Text Control packages
- SMTP credentials for application e-mails
- A signing certificate, either:
  - local PFX uploaded through the admin portal, or
  - Azure Key Vault certificate configuration

## ⚙️ Configuration

The main configuration lives in `appsettings.json`.

Important sections:

- `ConnectionStrings:IdentityLiteDB`: LiteDB Identity database connection.
- `AppSettings:DataDirectory`: base application data directory.
- `AppSettings:DatabaseDirectory`: base database directory.
- `AppSettings:AuditLogsPath`: audit log output directory.
- `AppSettings:SigningCertificate`: local PFX or Azure Key Vault signing configuration.
- `AppSettings:EmailTemplatesPath`: folder containing HTML e-mail templates.
- `AppSettings:AllowedFileTypes`: upload file extensions allowed by the application.
- `Credentials:EMail`: SMTP settings used for invitations, 2FA codes, signing notifications, and status e-mails.
- `BootstrapAdmin:Email`: first admin bootstrap e-mail.
- `Authentication:Bearer`: OAuth 2.0/OIDC JWT bearer token validation settings for the Web API.
- `Authentication:LocalOAuth`: local client-credentials token endpoint settings and API clients.
- `Authentication:OpenIdConnect`: optional browser SSO login settings.

Do not commit real SMTP passwords, production database passwords, PFX files, or Key Vault client secrets. Use user secrets, environment variables, deployment configuration, or another secure secret provider for production values.

## 🚀 First Run

1. Open `SignFabric.sln` in Visual Studio.
2. Review `appsettings.json` and set local development paths and connection strings.
3. Start the application from Visual Studio.
4. Register or create the initial user.
5. Make sure an admin user exists. The configured `BootstrapAdmin:Email` is promoted when matching the first admin bootstrap rules.
6. Open `/admin`.
7. Configure SMTP settings in **E-Mail Settings**.
8. Configure a signing certificate in **Certificate Management**.
9. Optional: configure SSO, external bearer tokens, or local OAuth API clients in **Authentication**.

The admin page shows warnings when no active signing certificate or no SMTP credentials are configured. If an encrypted SMTP password can no longer be decrypted after an application identity change, the admin page asks you to re-enter the password instead of failing during page load.

## 🔐 Signing Certificates

The application requires an active signing certificate before users can request signatures.

Supported providers:

- **Local PFX**: uploaded in the admin portal. PFX passwords are stored encrypted.
- **Azure Key Vault**: configured in the admin portal. The app can use default Azure credentials or configured tenant/client settings.

The signing flow intentionally does not fall back to unsigned PDF generation when no certificate is available.

## 📧 E-Mail

SMTP settings are managed in the admin portal and persisted to application settings. Stored SMTP passwords are encrypted. If the application identity changes and the old protected SMTP password cannot be decrypted, re-enter the password in **Admin > E-Mail Settings** to encrypt it again.

HTML e-mail templates are stored in `App_Data` by default and can be edited in the admin portal. Templates use a shared layout plus child templates, and placeholder tokens such as `%%%url%%%` are merged during sending.

## 👤 Identity and Users

The application uses ASP.NET Core Identity with LiteDB-backed users and roles as the local application identity layer.

Current roles:

- `Admin`: full administration access, including user management, certificate management, SMTP/e-mail templates, and authentication settings.
- `User`: standard authenticated sender account. Users can create envelopes, manage templates/contracts, send signing requests, and access their own documents.
- `Signer`: restricted account for recipients who signed without logging in. Signers can only view and download completed documents where their e-mail address is a signed recipient. They cannot create envelopes, templates, or contracts.

Signer account creation is globally controlled in **Admin > Authentication > Signer Accounts**. When enabled, the post-signing confirmation dialog can offer recipients a restricted signer account so they can access their completed documents later. When disabled, signing ends with the signed confirmation dialog and no account prompt is shown.

User management includes:

- creating users with temporary passwords
- first name and name profile claims
- role assignment
- account enable/disable
- e-mail based 2FA
- user deletion

The login flow supports both local e-mail/password users and optional OpenID Connect SSO. External identities are linked to local SignFabric users so roles, profile data, and per-user envelope storage remain consistent.

## 💾 Data Storage

Identity data is stored in the configured LiteDB identity database.

Application data is stored under the configured data paths. User-specific files and databases are organized in per-user folders under `Data/users`.

Audit logs are written to the configured audit log path.

## 🎨 Frontend Assets

Frontend source files:

- `wwwroot/css/site.css`
- `wwwroot/js/esign/esign-query.js`
- `wwwroot/js/esign/esign-core.js`
- `wwwroot/js/esign/esign-bindings.js`
- `wwwroot/js/esign/esign-page-init.js`

Bundles are configured in `bundleconfig.json`:

- `wwwroot/css/site.min.css`
- `wwwroot/js/site.min.js`

## 🏗️ Build

From the project directory:

```powershell
dotnet restore
dotnet build .\SignFabric.csproj
```

The build runs the bundler configured in `bundleconfig.json`.

## 🛠️ Development Notes

- Keep user data, certificates, audit logs, and generated databases out of source control unless explicitly needed for a test fixture.
- Do not store plain text PFX or SMTP passwords.
- Prefer the existing application abstractions in `Application/` and `Infrastructure/` when adding workflows.
- Keep e-mail template subjects, preheaders, and HTML editable through the admin portal.
- When changing frontend source files, verify that `site.min.js` and `site.min.css` are regenerated by the bundler.

## 🧭 Important Routes

- `/dashboard`
- `/envelopes`
- `/envelopes/create`
- `/contracts`
- `/contracts/create`
- `/templates`
- `/templates/create`
- `/review`
- `/review/validate`
- `/admin`

## 🔌 Envelope Web API

The external API uses resource-oriented, versioned route names:

- `POST /api/v1/envelopes`: create an envelope from a Base64 encoded document, attach signer metadata, validate signature boxes, and optionally send immediately.
- `GET /api/v1/envelopes`: retrieve all envelopes owned by the authenticated API caller.
- `GET /api/v1/envelopes/{id}`: retrieve envelope details and signer status.
- `GET /api/v1/envelopes/{id}/status`: retrieve only status-oriented envelope information.

Example create request:

```json
{
  "fileName": "agreement.tx",
  "documentBase64": "base64-encoded-document",
  "sendImmediately": true,
  "signers": [
    {
      "id": "signer1",
      "name": "Jane Doe",
      "email": "jane@example.com"
    }
  ]
}
```

The uploaded document must already contain TX Text Control signature fields for every signer. Each field must be named with this pattern:

```text
txsign_{signerId}
```

For the example above, the document must contain a signature field named `txsign_signer1`.

The API is protected with OAuth 2.0/OIDC JWT bearer authentication. Browser cookies are intentionally not accepted for `/api/v1/envelopes`.

Configure bearer token validation with:

```json
{
  "Authentication": {
    "Bearer": {
      "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
      "Audience": "api://your-api-client-id",
      "RequireHttpsMetadata": true
    }
  }
}
```

API callers must send:

```http
Authorization: Bearer {access_token}
```

Required permissions:

- `POST /api/v1/envelopes`: `envelopes:create`
- `GET /api/v1/envelopes`: `envelopes:read` or `envelopes:create`
- `GET /api/v1/envelopes/{id}`: `envelopes:read` or `envelopes:create`
- `GET /api/v1/envelopes/{id}/status`: `envelopes:read` or `envelopes:create`

The app accepts these permissions from either space-delimited OAuth scopes in `scp` / `scope`, or app roles in `roles`. This supports both delegated user flows and client credentials flows.

For Microsoft Entra ID, model this as an API app registration with exposed scopes/app roles. For server-to-server integrations, prefer the client credentials flow with app roles such as `envelopes:create` and `envelopes:read`.

### 🎟️ Local OAuth Token Endpoint

If no external OAuth/OIDC provider is available, SignFabric can issue local client-credentials access tokens.

Admins can create and remove local OAuth API clients in **Admin > Authentication**. The portal generates the client id and one-time client secret, stores only the SHA-256 secret hash, and assigns the client to the local user whose envelope store should own API-created envelopes.

## Recent Changes

### Editor and Sidebar UX

- Reworked the envelope editor sidebar to match the newer template and contract editor style.
- Added clearer section headings and placement hints for recipient signatures and recipient fields.
- Updated the primary editor action from a generic save label to a clearer **Done Editing** flow.
- Added consistent sidebar signature button states:
  - green when a signature field exists in the document
  - yellow when that signature field is currently selected
- Switched sidebar signature state updates to TX Text Control signature field events:
  - `signatureFieldCreated`
  - `signatureFieldDeleted`
  - `signatureFieldSelected`
  - `signatureFieldDeselected`

### Signature Area Wizard

- Added an **Insert Signature Area** workflow in the envelope editor.
- Added a modal wizard for inserting structured signature blocks.
- Added recipient selection in the wizard so one signature block can be created for one or more selected signers.
- Added optional row toggles for:
  - signer name
  - current date
- Moved the wizard modal into the shared envelope editor partial so it is available in the active editor context.

### Signature Area Table Generation

- Implemented structured signature block creation as one table per selected signer.
- Added a paragraph break between signer tables to separate multi-signer signature areas.
- Added dynamic table plans so unchecked optional fields are not inserted as empty rows.
- Current signature area row order:
  1. Company
  2. Signature
  3. Current Date (optional)
  4. Signer Name (optional)
  5. Title
- Company and Title rows insert a label followed by a line break and a text form field.
- Signature rows insert a TX Text Control signature field.
- Signature insertion prefers inline mode when the runtime supports it and falls back to anchored insertion when needed.

### Signature Area Styling

- Applied the `Normal` formatting style before signature area insertion.
- Set the insertion font size to 12pt for generated signature blocks.
- Added signature area table formatting with:
  - outer box border
  - emphasized signature underline
  - cell spacing and padding
- Adjusted the signature button styling to use inset outlines so borders render evenly.

### Table Handling and Stability

- Changed multi-signer signature area insertion to a two-phase flow:
  1. create all signer tables first
  2. populate each table afterward
- Avoided stale table adapter issues by re-fetching tables by ID before populating them.
- Reworked table ID allocation to scan existing table IDs once and reserve unique IDs locally for the current insertion run.
- Avoided relying on table count for ID generation, which could collide with existing or non-contiguous table IDs.

### Table Cell Insertion Fixes

- Added explicit cell selection helpers for table row content insertion.
- Normalized the TX Text Control cell-start behavior so content is inserted at the correct cell input position.
- Explicitly collapsed the selection before inserting text, merge fields, form fields, and signature fields into table cells.
- Fixed content bleeding and mixed-row insertion issues in multi-table signature area generation.

### Signature Box Validation

- Updated document signature validation to use `tx.SignatureFields` directly on the server side.
- Removed the older frame-based signature box detection logic.
- Kept envelope workflow validation aligned with the actual TX signature field objects used by the editor.

### Recipient Assignment Workflow

- Added support for detecting unassigned recipient-owned fields from templates.
- Extended recipient assignment handling to include:
  - form fields
  - auto-fill fields such as signer name and signer email
  - signature fields
- Added automatic assignment when only one signer exists.
- Added a modal assignment step when multiple signers exist and fields are still unassigned.

### Template and Contract Flow Improvements

- Aligned template and contract create/edit flows more closely with the updated envelope workflow.
- Improved the use of thumbnails in creation and edit flows.
- Added thumbnail refresh after editing templates and contracts.
- Added template field assignment handling when creating envelopes from templates.
- Added the path from template to contract creation.
- Added recipient preselection when moving accepted contracts into a signature flow.

### Error Handling and Messaging

- Added popup-style modal error handling for invalid document uploads and unsupported conversions.
- Reused the existing modal visual style instead of showing toasts.
- Improved TX document conversion error handling in `TextControlHelpers`.
- Removed leftover toast feedback in the field assignment flow to keep the UX aligned with modal-based messaging.

### Cleanup

- Removed obsolete toast markup from the affected editor and workflow views.
- Removed an unused signature field event helper after switching to direct selected-field lookup.
- Kept new document operations behind the existing `ITxDocumentService` abstraction and `TxDocumentService` implementation.

Configure `Authentication:LocalOAuth`:

```json
{
  "Authentication": {
    "LocalOAuth": {
      "Enabled": true,
      "Issuer": "SignFabric",
      "Audience": "signfabric-api",
      "SigningKey": "at-least-32-characters-long-signing-key",
      "AccessTokenMinutes": 60,
      "Clients": [
        {
          "ClientId": "integration-app",
          "DisplayName": "Integration App",
          "SecretSha256": "sha256-hex-lowercase-client-secret-hash",
          "UserId": "internal-or-service-user-id",
          "Scopes": [
            "envelopes:create",
            "envelopes:read"
          ]
        }
      ]
    }
  }
}
```

`SigningKey` and client secrets must not be committed. Store them in user secrets, environment variables, or deployment configuration.

Generate a client secret hash:

```http
POST /api/v1/oauth/secret-hash
Content-Type: application/json

{
  "clientSecret": "plain-client-secret"
}
```

Request an access token:

```http
POST /api/v1/oauth/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&
client_id=integration-app&
client_secret=plain-client-secret&
scope=envelopes:create envelopes:read
```

Response:

```json
{
  "access_token": "eyJ...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "scope": "envelopes:create envelopes:read"
}
```

Use the returned token for Web API calls:

```http
Authorization: Bearer eyJ...
```

The configured `UserId` determines which envelope store the API client uses. Use a real local user id if envelopes should appear for that user, or a dedicated service user id for isolated integrations.

## 🪪 OpenID Connect Login

The application supports hybrid authentication:

- local e-mail/password login through ASP.NET Core Identity
- OpenID Connect login through an external identity provider

OpenID Connect is disabled by default. Enable and configure it with:

```json
{
  "Authentication": {
    "OpenIdConnect": {
      "Enabled": true,
      "DisplayName": "Single Sign-On",
      "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
      "ClientId": "your-client-id",
      "ClientSecret": "your-client-secret",
      "CallbackPath": "/signin-oidc",
      "SignedOutCallbackPath": "/signout-callback-oidc",
      "ResponseType": "code",
      "SaveTokens": true,
      "AutoProvisionUsers": false,
      "Scopes": [
        "openid",
        "profile",
        "email"
      ]
    }
  }
}
```

When enabled, the login page displays the configured external provider next to the local login form.

The OpenID Connect callback links the external identity to a local SignFabric user. If no local user exists, `AutoProvisionUsers` controls whether the app creates one automatically. For controlled enterprise rollout, keep `AutoProvisionUsers` set to `false` and create users in the admin portal first.

Do not commit the OpenID Connect client secret. Store it in user secrets, environment variables, or deployment configuration.

Authentication settings can also be managed in the admin portal under **Admin > Authentication**. Changes to authentication schemes are read at application startup, so restart the application after saving OIDC, bearer, or local OAuth settings.
