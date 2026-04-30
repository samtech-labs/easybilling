# EasyBilling — CLAUDE.md

## Project Overview

EasyBilling is a Romanian invoicing/billing SaaS application.
Live at **easybilling.ro**, hosted on a **Hetzner VPS** (Linux, systemd).
Owned and operated by **Samtech Labs SRL** (VAT-registered, CAEN 6201).

Features: multi-company management, invoice creation (including credit notes), multi-currency (RON/EUR),
membership tiers with invoice limits, PDF generation, and full integration with Romania's ANAF tax authority
for e-invoicing (eFactura) and company data lookup.

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
`InvoiceAnafSubmission`, `Membership`, `MembershipType`, `BankAccount`

**Enums:**
- `UserRole` — `ADMIN`, `USER`, `ACCOUNTANT`
- `InvoiceType` — `Invoice`, `CreditNote`
- `Currency` — `RON`, `EUR`

**Key domain rules:**
- Credit notes reference the original invoice via `OriginalInvoiceId` (restrict delete — no orphaned credit notes)
- Invoice series/number must be unique per company per fiscal year
- VAT rates: 19%, 9%, 5%, 0% (scutit), reverse charge (taxare inversa)
- Multi-currency: RON and EUR supported; foreign currency invoices require exchange rate in UBL XML
- Invoice line unit of measure is user-configurable (buc, ore, zi, luna, kg, m, mp, l, elem, set, etc.) — stored on each `InvoiceLine`
- Bank accounts belong to a company (one-to-many); store bank name, IBAN, and currency (RON/EUR)

### EasyBilling.Application
Business logic layer — **Repository + Service pattern** (no CQRS/MediatR).

```
Services/
  InvoiceService, EFacturaService, AnafIntegrationService
  ClientService, CompanyService, UserService
  MembershipService, MembershipTypeService, InvoiceAnafSubmissionService
  BankAccountService
Interfaces/
  Interfaces/Repositories/    ← repository contracts
  Interfaces/Services/        ← service contracts
Dtos/                         ← response DTOs + pagination filters
Requests/                     ← create/update request models
Jobs/
  AnafStatusCheckJob          ← Hangfire job; polls ANAF status, retries up to 20 times
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
Middleware/
  UserContextMiddleware       ← populates scoped UserContext from JWT claims
Migrations/                   ← EF Core migrations (startup project: EasyBilling.Presentation)
```

### EasyBilling.Presentation
ASP.NET Core host. **All DI registrations live in `Program.cs`** (no extension method modules).

```
Controllers/
  Auth, Invoice, Company, Client, User, Membership, MembershipType, AnafIntegration, BankAccount
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
  ← Maps Romanian unit names to UN/ECE Recommendation 20 codes via MapUnitCode()
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

### Unit of Measure in UBL XML

The `cbc:InvoicedQuantity` element requires a `unitCode` attribute using **UN/ECE Recommendation 20** codes.
`EFacturaXmlGenerator.MapUnitCode()` maps Romanian unit names to these codes:

| Romanian unit | UBL `unitCode` | Description |
|---------------|----------------|-------------|
| `buc` / `bucata` / `bucati` | `H87` | Piece |
| `ora` / `ore` | `HUR` | Hour |
| `zi` / `zile` | `DAY` | Day |
| `luna` / `luni` | `MON` | Month |
| `kg` / `kilogram` | `KGM` | Kilogram |
| `l` / `litru` / `litri` | `LTR` | Litre |
| `m` / `metru` / `metri` | `MTR` | Metre |
| `mp` / `m2` | `MTK` | Square metre |
| *(fallback)* | `H87` | Defaults to piece |

⚠️ When adding new units, ensure the `unitCode` exists in the UN/ECE Rec 20 code list — ANAF validates this.
Common additions that may be needed: `SET` (set), `EA` (each), `XPK` (package), `C62` (dimensionless unit).

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
- Next.js App Router; prefer server components; use `use client` only when required
- All API calls go through `lib/api.ts` — never raw `fetch` in components
- Form state: `react-hook-form` + `zod` validation
- TypeScript strict mode — no `any`
- Error boundaries at route level

### General
- Feature branches off `main`; PRs for all changes
- No hardcoded CIFs, IBANs, or ANAF credentials anywhere in the codebase

---

## Current Active Work

> **Update this section at the start of each session.**

### Configurable Invoice Line Unit of Measure

**Goal:** Allow users to set the unit of measure per invoice line (currently defaults to "buc").
Common Romanian units: buc (piece), ore (hours), zile (days), luni (months), elem (element), set, kg, m, mp, l.

**Scope — changes required across all layers:**

#### Domain (`EasyBilling.Domain`)
- `InvoiceLine.Unit` property already exists as `string`
- Add a `UnitOfMeasure` constants class or enum with predefined values + allow freetext
- Default value should remain `"buc"` for backward compatibility

#### Application (`EasyBilling.Application`)
- `CreateInvoiceLineRequest` / `UpdateInvoiceLineRequest` — expose `Unit` field (string, optional, defaults to `"buc"`)
- `InvoiceLineDto` — include `Unit` in response DTO
- `InvoiceService` — pass `Unit` through when creating/updating lines; validate against known units or accept freetext
- FluentValidation: `Unit` should not be empty; max length ~20 chars

#### ANAF Integration (`EasyBilling.ANAFIntegration`)
- `EFacturaXmlGenerator.MapUnitCode()` already handles the Romanian-to-UBL mapping
- Verify the mapping covers all units exposed in the UI; extend if needed (e.g. `elem` → `C62`, `set` → `SET`)
- Unit tests for `MapUnitCode` — ensure every UI-exposed unit maps to a valid UBL code

#### Presentation (`EasyBilling.Presentation`)
- Invoice controller already passes through request models — no changes expected unless adding a `GET /units` lookup endpoint
- Consider a `GET /api/units` endpoint returning the list of supported units with labels (for frontend dropdown)

#### Frontend (Next.js)
- Invoice create/edit form: add a unit selector (dropdown/combobox) per invoice line, defaulting to "buc"
- Populate options from API or hardcode the common set: `buc`, `ore`, `zile`, `luni`, `kg`, `m`, `mp`, `l`, `elem`, `set`
- Invoice list/detail views: display the unit alongside quantity
- PDF preview: unit already renders from `InvoiceLine.Unit`

#### QuestPDF (Invoice PDF Generation)
- Verify `InvoiceLine.Unit` is rendered in the quantity column of the PDF template
- If currently hardcoded to "buc", update to use the line's `Unit` value

#### Migration
- If `InvoiceLine.Unit` column already exists in DB with no default, add a migration setting default to `'buc'`
- Backfill existing rows: `UPDATE invoice_lines SET unit = 'buc' WHERE unit IS NULL`

### Company Bank Accounts — New Entity + Full CRUD

**Goal:** Users can manage bank accounts per company. These will later be selectable on invoices (supplier IBAN in UBL XML).

#### Domain (`EasyBilling.Domain`)

New entity `BankAccount`:

```csharp
public class BankAccount
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; }
    public string BankName { get; set; }    // e.g. "ING Bank", "BCR", "BT"
    public string Iban { get; set; }        // RO-prefixed IBAN, 24 chars
    public Currency Currency { get; set; }  // RON or EUR (reuse existing enum)
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

- Relationship: `Company` has many `BankAccount` (one-to-many)
- Add `ICollection<BankAccount> BankAccounts` navigation property on `Company`
- A company can have multiple accounts (e.g. one RON, one EUR)
- No unique constraint on IBAN per company — user may have multiple accounts at same bank

#### Infrastructure (`EasyBilling.Infrastructure`)

**AppDbContext** — Fluent API configuration:
- `CompanyId` FK with cascade delete (deleting a company removes its bank accounts)
- `BankName`: required, max length 100
- `Iban`: required, max length 34 (ISO 13616 max)
- `Currency`: stored as string conversion (same pattern as existing `Currency` enum usage)
- Index on `CompanyId` for fast lookups

**Repository:**
- `IBankAccountRepository` interface in `Application/Interfaces/Repositories/`
- `BankAccountRepository` implementation in `Infrastructure/Repositories/`
- Methods: `GetByCompanyAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`
- All queries scoped to company via `UserContext` — never return accounts from other companies

**Migration:**
- `dotnet ef migrations add AddBankAccount --project EasyBilling.Infrastructure --startup-project EasyBilling.Presentation`

#### Application (`EasyBilling.Application`)

**DTOs:**
- `BankAccountDto` — `Id`, `BankName`, `Iban`, `Currency`, `CreatedAt`, `UpdatedAt`

**Requests:**
- `CreateBankAccountRequest` — `BankName` (required), `Iban` (required), `Currency` (required)
- `UpdateBankAccountRequest` — same fields as create

**Validation (FluentValidation):**
- `BankName`: not empty, max 100 chars
- `Iban`: not empty, max 34 chars, must match Romanian IBAN format (`^RO\d{2}[A-Z]{4}[A-Za-z0-9]{16}$`)
- `Currency`: must be valid `Currency` enum value

**Service:**
- `IBankAccountService` / `BankAccountService`
- Methods: `GetByCompanyAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`
- All methods scoped to active company via `UserContext`
- Throw if bank account belongs to a different company (guard against IDOR)

**DI Registration:** Add repository + service in `Program.cs`

#### Presentation (`EasyBilling.Presentation`)

**Controller:** `BankAccountController`
- `GET    /api/bank-accounts`          — list all for active company
- `GET    /api/bank-accounts/{id}`     — get single
- `POST   /api/bank-accounts`          — create
- `PUT    /api/bank-accounts/{id}`     — update
- `DELETE /api/bank-accounts/{id}`     — delete
- All endpoints require `[Authorize]` + `HasActiveMembership` policy
- Thin controller — delegate everything to `IBankAccountService`

### Bank Account Selection on Invoice Creation

**Goal:** When creating an invoice, after the user selects a company, fetch that company's bank accounts so the user can pick one. The selected bank account's IBAN and bank name flow through to the invoice PDF and the ANAF e-Factura UBL XML.

**This touches the full invoice pipeline — be careful with every layer.**

#### New Endpoint — Fetch Bank Accounts by Company

- `GET /api/bank-accounts?companyId={companyId}` — or reuse existing `GET /api/bank-accounts` since it's already scoped to active company via `UserContext`
- The frontend calls this **after company selection** on the invoice create/edit form to populate the bank account dropdown
- Response: list of `BankAccountDto` (`id`, `bankName`, `iban`, `currency`)

#### Domain (`EasyBilling.Domain`)

- Add `Guid? BankAccountId` FK on `Invoice` entity (nullable — existing invoices won't have one)
- Add `BankAccount? BankAccount` navigation property on `Invoice`
- Relationship: optional many-to-one (`Invoice` → `BankAccount`), **restrict delete** — cannot delete a bank account that is referenced by invoices (or set null on delete, decide which is safer)
- ⚠️ Do NOT cascade delete — deleting a bank account must not delete invoices

#### Infrastructure (`EasyBilling.Infrastructure`)

**AppDbContext — Fluent API:**
- Configure `Invoice.BankAccountId` as optional FK to `BankAccount`
- Delete behavior: `DeleteBehavior.Restrict` (prevent deleting bank accounts linked to invoices) or `SetNull` (clear the reference)
- No index needed beyond the FK default

**Migration:**
- `dotnet ef migrations add AddBankAccountToInvoice --project EasyBilling.Infrastructure --startup-project EasyBilling.Presentation`
- Adds nullable `BankAccountId` column to `invoices` table — no backfill needed (existing invoices get `NULL`)

#### Application (`EasyBilling.Application`)

**Requests:**
- `CreateInvoiceRequest` / `UpdateInvoiceRequest` — add `Guid? BankAccountId` field (optional)
- Validation: if provided, must reference a `BankAccount` that belongs to the **same company** as the invoice (IDOR guard)

**DTOs:**
- `InvoiceDto` — add `BankAccountId`, `BankAccountBankName`, `BankAccountIban` (flatten for frontend convenience, avoid nested DTO if not used elsewhere)

**InvoiceService:**
- On create/update: if `BankAccountId` is provided, load the `BankAccount`, verify it belongs to the same company, attach to invoice
- If `BankAccountId` is null, invoice proceeds without bank details (backward compatible)
- On invoice GET: eager-load or project the bank account fields into the DTO

#### ANAF Integration (`EasyBilling.ANAFIntegration`)

**This is the critical part.** The `EFacturaXmlGenerator` already has a `CreatePaymentMeans(PaymentInfo, currencyCode)` method that generates:

```xml
<cac:PaymentMeans>
    <cbc:PaymentMeansCode>42</cbc:PaymentMeansCode>  <!-- 42 = bank transfer -->
    <cac:PayeeFinancialAccount>
        <cbc:ID>{IBAN}</cbc:ID>                       <!-- from BankAccount.Iban -->
        <cac:FinancialInstitutionBranch>
            <cbc:Name>{BankName}</cbc:Name>           <!-- from BankAccount.BankName -->
        </cac:FinancialInstitutionBranch>
    </cac:PayeeFinancialAccount>
</cac:PaymentMeans>
```

**Changes needed:**
- The `PaymentInfo` object (or whatever DTO feeds into `CreatePaymentMeans`) must be populated from `Invoice.BankAccount` when present
- Map: `PaymentInfo.PaymentAccountId` ← `BankAccount.Iban`, `PaymentInfo.FinancialInstitutionName` ← `BankAccount.BankName`
- `PaymentMeansCode` = `"42"` (credit transfer / bank transfer) — this is correct for Romanian B2B invoices
- If no bank account is selected (`BankAccountId` is null), either:
  - Omit `PaymentMeans` from XML entirely (allowed by ANAF for some invoice types), OR
  - Keep existing fallback behavior (if any hardcoded values exist today, preserve them)
- ⚠️ ANAF validates that `PayeeFinancialAccount/ID` is a valid IBAN format when present — the FluentValidation on `BankAccount.Iban` already covers this
- ⚠️ For credit notes: if the original invoice had a bank account, the credit note should reference the same one (or allow override)

#### QuestPDF (Invoice PDF Generation)

- If `Invoice.BankAccount` is present, render bank details on the PDF:
  - Bank name, IBAN, currency — typically in the footer or payment details section
  - Format IBAN with spaces for readability: `RO49 AAAA 1B31 0075 9384 0000`
- If no bank account selected, omit the section (same as current behavior)

#### Presentation (`EasyBilling.Presentation`)

- `InvoiceController` — no structural changes; `CreateInvoiceRequest` already flows through
- Ensure `BankAccountId` is included in Swagger docs / request examples

#### Key Risks & Edge Cases

- **Company mismatch:** User creates invoice for Company A but passes a `BankAccountId` belonging to Company B → service must reject with 400/403
- **Deleted bank account:** If using `SetNull` delete behavior, invoices lose their bank reference after deletion — acceptable if the IBAN was already baked into the submitted XML. If using `Restrict`, user must unlink bank account from all invoices before deleting it
- **Already-submitted invoices:** Changing the bank account on an invoice that was already sent to ANAF has no effect on the submitted XML — but the UI should warn or prevent editing bank account on submitted invoices
- **Currency mismatch:** A RON invoice with a EUR bank account is technically valid (payment can be in a different currency than the invoice) — don't block this, but consider a UI warning

### Other Active Items
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