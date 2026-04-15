# EasyBilling — CLAUDE.md

## Project Overview

EasyBilling is a Romanian invoicing/billing SaaS application.
Live at **easybilling.ro**, hosted on a **Hetzner VPS** (Linux, systemd).
Owned and operated by **Samtech Labs SRL** (VAT-registered, CAEN 6201).

Features: multi-company management, invoice creation (including credit notes), multi-currency (RON/EUR),
membership tiers with invoice limits, PDF generation, billable hours tracking with automated invoice generation,
and full integration with Romania's ANAF tax authority for e-invoicing (eFactura) and company data lookup.

---

## Build & Run Commands

```bash
# Build the entire solution
dotnet build

# Run the main web API
dotnet run --project EasyBilling.Presentation
# http://localhost:5009  |  https://localhost:5001  |  Swagger: /swagger

# Run tests (xunit)
dotnet test --project EasyBillings.Tests

# EF Core migrations
dotnet tool restore
dotnet ef migrations add <Name> --project EasyBilling.Infrastructure --startup-project EasyBilling.Presentation
dotnet ef database update --project EasyBilling.Infrastructure --startup-project EasyBilling.Presentation
```

---

## Tech Stack

### Backend
- **.NET 10** — ASP.NET Core Web API
- **PostgreSQL** via EF Core (Npgsql)
- **JWT Bearer** — authentication
- **Hangfire** (PostgreSQL-backed) — background jobs
- **QuestPDF** — invoice PDF generation
- **FluentValidation** — request validation
- **Newtonsoft.Json** — JSON serialization

### Frontend
- **Next.js** (React, TypeScript) — separate app; CORS-allowed on `localhost:3000` and `easybilling.ro`

### Infrastructure
- **Hetzner VPS** — Linux, systemd services
- **Azure DevOps** — CI/CD pipelines
- **Cloudflare** — DNS, proxy
- **Nginx** — reverse proxy for frontend + API

---

## Architecture

**Clean Architecture** — 5 active projects + 1 legacy. Dependency flow:

```
Presentation  →  Application  →  Domain
Infrastructure  →  Application  →  Domain
ANAFIntegration  (standalone, referenced by Application)
```

### EasyBilling.Domain
Pure domain models, no external dependencies.

**Entities:** `User`, `Company`, `Client`, `Invoice`, `InvoiceLine`, `AnafToken`,
`InvoiceAnafSubmission`, `Membership`, `MembershipType`, `TimeEntry`, `ClientRate`,
`EmailTemplate`, `InvoiceGenerationBatch`

**Enums:**
- `UserRole` — `ADMIN`, `USER`, `ACCOUNTANT`
- `InvoiceType` — `Invoice`, `CreditNote`
- `InvoiceStatus` — `Draft`, `Approved`, `Sent`, `Cancelled`
- `BatchStatus` — `Draft`, `Approved`, `Sent`, `Failed`
- `Currency` — `RON`, `EUR`

**Key domain rules:**
- Credit notes reference the original invoice via `OriginalInvoiceId` (restrict delete — no orphaned credit notes)
- Invoice series/number must be unique per company per fiscal year
- Invoice.Status defaults to `Approved` for manually created invoices (backward compat); draft invoices from batch generation get `Draft` and have no Series/Number until approved
- VAT rates: 19%, 9%, 5%, 0% (scutit), reverse charge (taxare inversa)
- Multi-currency: RON and EUR supported; foreign currency invoices require exchange rate in UBL XML
- TimeEntry: unique per (CompanyId, ClientId, Date); hours must be > 0 and total per day across all clients <= 24
- ClientRate: effective-dated per client; the rate with the latest `EffectiveFrom <= targetDate` applies
- EmailTemplate: one per client; supports placeholders `{InvoiceNumber}`, `{InvoiceSeries}`, `{Month}`, `{Year}`, `{ClientName}`, `{TotalAmount}`, `{Currency}`, `{DueDate}`

### EasyBilling.Application
Business logic layer — **Repository + Service pattern** (no CQRS/MediatR).

```
Services/
  InvoiceService, EFacturaService, AnafIntegrationService
  ClientService, CompanyService, UserService
  MembershipService, MembershipTypeService, InvoiceAnafSubmissionService
  TimeTrackingService         ← billable hours CRUD + monthly summaries
  ClientRateService           ← hourly rate management per client (effective-dated)
  EmailTemplateService        ← per-client email template CRUD + placeholder rendering
  InvoiceGenerationService    ← draft invoice creation from time entries, batch approve/reject
Interfaces/
  Interfaces/Repositories/    ← repository contracts
  Interfaces/Services/        ← service contracts (incl. IEmailService, ISmsService, IHolidayService)
Dtos/                         ← response DTOs + pagination filters
Requests/                     ← create/update request models
Jobs/
  AnafStatusCheckJob          ← Hangfire job; polls ANAF status, retries up to 20 times
  MonthlyInvoiceGenerationJob ← Hangfire recurring; runs daily, triggers on last working day of month
  InvoiceSendJob              ← Hangfire job; generates PDF, sends email, updates status
Helpers/
  AnafIntegrationHelper       ← XML generation + ANAF response mapping
```

### EasyBilling.Infrastructure
Data access and external concerns.

```
Persistence/
  AppDbContext                ← Fluent API config (cascade deletes, indexes, relationships)
Repositories/                 ← concrete implementations of all repository interfaces
Services/
  AuthService                 ← JWT token generation
  CurrentUserService          ← extracts user from HttpContext
  SmtpEmailService            ← plain text email with PDF attachment (implements IEmailService)
  SmsLinkService              ← SMSLink.ro REST API integration (implements ISmsService)
  HolidayService              ← dual-API Romanian holiday lookup with caching (implements IHolidayService)
Middleware/
  UserContextMiddleware       ← populates scoped UserContext from JWT claims
Migrations/                   ← EF Core migrations (startup project: EasyBilling.Presentation)
```

### EasyBilling.Presentation
ASP.NET Core host. **All DI registrations live in `Program.cs`** (no extension method modules).

```
Controllers/
  Auth, Invoice, Company, Client, User, Membership, MembershipType, AnafIntegration
  TimeTracking                ← billable hours CRUD (admin-only)
  ClientRate                  ← hourly rate management (admin-only)
  EmailTemplate               ← per-client email template management (admin-only)
  InvoiceBatch                ← batch generation, approval, rejection (admin-only)
Authorization/
  CanCreateInvoice            ← enforces invoice limit per membership tier
  CanUseEFactura              ← gates e-invoice feature by membership tier
  HasActiveMembership         ← requires active subscription
```

- Hangfire dashboard: `/hangfire` (development only)
- Swagger UI: `/swagger`
- CORS origins: `localhost:3000`, `easybilling.ro`

### EasyBilling.ANAFIntegration
Standalone project for ANAF API integration.

```
EFactura/
  ← Upload UBL 2.1 XML + download signed responses
  ← Supports test (api.anaf.ro/test/) and production (api.anaf.ro/prod/) endpoints
PublicGeneralAPI/
  ← Company lookup by CUI (tax ID) via ANAF public REST API
EFacturaXmlGenerator
  ← Converts domain Invoice entities → UBL 2.1 XML
```

### EasyBilling.Api
Legacy/placeholder project (weather forecast template only). **Not the active API — ignore.**

### EasyBillings.Tests
xunit — currently targets **net9.0** (all other projects target net10.0).

---

## Auth & Multi-tenancy

- Users register and create (or are invited to) a **Company**
- `UserContextMiddleware` populates a scoped `UserContext` from JWT claims on every request
- Every service call is scoped to the active company via `UserContext`
- **ANAF OAuth tokens are per-user** — stored in `AnafToken` entity linked to `User`
- Authorization is policy-based: `CanCreateInvoice`, `CanUseEFactura`, `HasActiveMembership`

---

## ANAF Integration

### OAuth Flow
- Authorization: `https://logincert.anaf.ro/anaf-oauth2/v1/authorize`
- Token: `https://logincert.anaf.ro/anaf-oauth2/v1/token`
- Tokens stored per-user in `AnafToken`; refresh handled server-side before each API call
- Redirect URI must exactly match the registration in the ANAF SPV portal
- ⚠️ OAuth callback can return errors in query params even on apparent "success" — always check `error` param before `code`

### e-Factura API Endpoints

| Action | Endpoint |
|--------|----------|
| Upload | `POST https://api.anaf.ro/prod/FCTEL/rest/upload?standard=UBL&cif={cif}` |
| Status check | `GET https://api.anaf.ro/prod/FCTEL/rest/stareMesaj?id_incarcare={id}` |
| Download | `GET https://api.anaf.ro/prod/FCTEL/rest/descarcare?id={id}` |

- Upload response contains `index_incarcare` — used for all subsequent status polling
- Status values: `in prelucrare`, `ok`, `nok` (error details in response XML)
- `AnafStatusCheckJob` (Hangfire) polls status automatically; retries up to **20 times**
- ⚠️ Staging environment (`api.anaf.ro/test/`) has different client IDs than production — never mix credentials

### UBL 2.1 XML Generation
- Schema: Romanian UBL 2.1 (EN 16931 with RO extensions)
- Root namespace: `urn:oasis:names:specification:ubl:schema:xsd:Invoice-2`
- Mandatory elements: `cbc:ID`, `cbc:IssueDate`, `cac:AccountingSupplierParty`, `cac:AccountingCustomerParty`, `cac:TaxTotal`, `cac:LegalMonetaryTotal`
- Credit notes: use `CreditNote` root element, reference original invoice via `cac:BillingReference`
- ⚠️ `TaxTotal` must appear at **both line level AND document level** (as a sum) — mismatch causes ANAF rejection
- ⚠️ `LegalMonetaryTotal/TaxInclusiveAmount` must equal `TaxExclusiveAmount + TaxTotal`
- Foreign currency invoices require exchange rate declared in XML

---

## Billable Hours & Auto-Invoicing

### Overview

End-to-end workflow: track billable hours per client in a monthly calendar → auto-generate draft invoices
at end of month → admin approves via SMS link → system sends PDF invoices via email.

### Data Flow

```
TimeEntry (hours/day/client)
  → InvoiceGenerationService groups by client, resolves ClientRate
    → Creates draft Invoice(s) with Status=Draft (no Series/Number)
      → InvoiceGenerationBatch groups the drafts
        → SMS notification sent to admin with approval link
          → Admin reviews at /invoices/batch/{id}/review
            → Approve: assigns Series/Number, Status=Approved, enqueues InvoiceSendJob
              → InvoiceSendJob: PDF generation → email via SmtpEmailService → Status=Sent
```

### Invoice Status Lifecycle

```
Manual creation:  → Approved (with Series/Number) → Sent → [to ANAF if eFactura active]
Batch generation: → Draft (no Series/Number) → Approved (assigns Series/Number) → Sent
                                              → Cancelled (if batch rejected)
```

⚠️ Draft invoices must NOT have Series/Number assigned — number sequencing happens on approval only.
⚠️ Existing manually created invoices default to `Status = Approved` (backward compatibility).

### Holiday Calculation

Last working day of the month is calculated by `HolidayService`:
- Calls both `api.bank-holidays.ro` AND `openholidaysapi.org` for Romanian public holidays
- Compares results, logs discrepancies, returns union (err on the side of "it's a holiday")
- Results cached in-memory per year
- `MonthlyInvoiceGenerationJob` runs daily at 08:00, checks if today is last working day

### External Integrations (New)

| Service | Purpose | Config |
|---------|---------|--------|
| SMTP (configurable) | Send invoice PDF emails | `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM_EMAIL` |
| SMSLink.ro | SMS notifications to admin | `SMSLINK_API_KEY` |
| api.bank-holidays.ro | Romanian public holidays (primary) | No auth required |
| openholidaysapi.org | Romanian public holidays (secondary) | No auth required |

### Permissions

Billable hours feature is **admin-only** in the first iteration: `[Authorize(Roles = "ADMIN")]` on all
new controllers (TimeTracking, ClientRate, EmailTemplate, InvoiceBatch). Granular permissions will be
designed later.

---

## Development Conventions

### Backend
- Controllers are thin — no business logic; only policy attribute decoration + service calls
- Services own all business logic; throw domain exceptions (`InvalidOperationException` or custom typed exceptions)
- EF Core migrations committed alongside model changes — **never hand-edit applied migrations**
- Use `decimal(18,2)` precision for all monetary amounts in EF Fluent API config
- Use `CancellationToken` on all async service and controller methods
- Sensitive config (ANAF credentials, DB connection string, JWT secret, encryption key) via environment variables only — never hardcoded

### Frontend
- Next.js App Router; all pages are client components (`'use client'`) — static export, no SSR
- All API calls go through React Query hooks in `hooks/` using `lib/api-client.ts` — never raw `fetch` or direct Axios in components
- Form state: vanilla `useState` — no form library
- TypeScript strict mode — no `any`
- Tailwind CSS only — no component library (shadcn, MUI, etc.)

### General
- Feature branches off `main`; PRs for all changes
- No hardcoded CIFs, IBANs, or ANAF credentials anywhere in the codebase

---

## Current Active Work

> **Update this section at the start of each session.**

### Billable Hours Feature (in progress)
- [ ] Phase 1: Domain entities (TimeEntry, ClientRate, EmailTemplate, InvoiceGenerationBatch) + Invoice Status enum + User.PhoneNumber
- [ ] Phase 1: EF Core config + migration
- [ ] Phase 1: Repository interfaces + implementations
- [ ] Phase 2: TimeTrackingService, ClientRateService, EmailTemplateService, InvoiceGenerationService
- [ ] Phase 2: HolidayService (dual-API with caching)
- [ ] Phase 3: SmtpEmailService, SmsLinkService, InvoiceSendJob, MonthlyInvoiceGenerationJob
- [ ] Phase 4: API controllers (TimeTracking, ClientRate, EmailTemplate, InvoiceBatch)
- [ ] Phase 5: Frontend — calendar UI, rate management, email templates, batch approval page
- [ ] Phase 6: Update existing InvoiceService for Status lifecycle + integration tests

### Existing work
- [ ] ANAF OAuth token refresh edge cases (concurrent requests, refresh race condition)
- [ ] Credit note XML — `BillingReference` mapping
- [ ] Membership invite flow (email invite → accept → role assignment)
- [ ] UI: invoice list filters (date range, status, series)
- [ ] UI: company settings — ANAF credentials connect/disconnect flow

---

## Environment Variables

```bash
# Backend
DATABASE_URL=postgresql://...
ANAF_CLIENT_ID=...
ANAF_CLIENT_SECRET=...
ANAF_REDIRECT_URI=https://easybilling.ro/auth/anaf/callback
JWT_SECRET=...
ENCRYPTION_KEY=...          # for encrypting stored ANAF tokens

# Email (SMTP)
SMTP_HOST=...               # e.g., smtp.gmail.com, mail.easybilling.ro
SMTP_PORT=587
SMTP_USERNAME=...
SMTP_PASSWORD=...
SMTP_FROM_EMAIL=facturi@easybilling.ro

# SMS (SMSLink.ro)
SMSLINK_API_KEY=...

# Frontend
NEXT_PUBLIC_API_URL=https://easybilling.ro/api
```

---

## Deployment

- **VPS:** Hetzner, Ubuntu, systemd services
- **Backend:** `dotnet publish` → systemd service
- **Frontend:** `next build` → served via Nginx
- **CI/CD:** Azure DevOps pipelines
- **Migrations:** run on deploy via `dotnet ef database update`
- **Logs:** systemd journal (no centralized logging yet)