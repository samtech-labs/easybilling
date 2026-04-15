# Billable Hours Feature — Implementation Tickets

> Feature: Track billable hours per client in a calendar UI, auto-generate invoices at end of month,
> send approval notification via SMS, then email PDF invoices to clients.

---

## Phase 1 — Domain & Data Layer (Backend)

### T-1.1: New domain entities + enums

**Scope:** `EasyBilling.Domain`

Create the following in `Models/`:

- **`TimeEntry`** — `Id (Guid)`, `CompanyId (FK)`, `ClientId (FK)`, `Date (DateOnly)`, `Hours (decimal)`
  - Constraint: unique composite on `(CompanyId, ClientId, Date)` — one entry per client per day
  - Constraint: `Hours > 0 && Hours <= 24`

- **`ClientRate`** — `Id (Guid)`, `ClientId (FK)`, `HourlyRate (decimal)`, `Currency (enum)`, `EffectiveFrom (DateOnly)`
  - Constraint: unique composite on `(ClientId, EffectiveFrom)` — one rate per effective date

- **`EmailTemplate`** — `Id (Guid)`, `ClientId (FK)`, `Subject (string)`, `Body (string)`, `ToEmail (string)`, `CcEmails (string, nullable)`, `BccEmails (string, nullable)`, `CreatedAt`, `UpdatedAt`

- **`InvoiceGenerationBatch`** — `Id (Guid)`, `CompanyId (FK)`, `Month (int)`, `Year (int)`, `GeneratedAt (DateTime)`, `Status (BatchStatus enum)`

Create in `Enums/`:

- **`InvoiceStatus`** — `Draft`, `Approved`, `Sent`, `Cancelled`
- **`BatchStatus`** — `Draft`, `Approved`, `Sent`, `Failed`

Modify existing:

- **`Invoice`** — add `Status (InvoiceStatus)`, `BatchId (Guid?, FK → InvoiceGenerationBatch)`
- **`User`** — add `PhoneNumber (string?, nullable)`

**Acceptance criteria:**
- All new entities follow existing patterns (Guid PKs, navigation properties)
- `decimal(18,2)` precision for all monetary/hours fields in Fluent API config
- No breaking changes to existing Invoice creation flow (Status defaults to `Approved` for manually created invoices)

---

### T-1.2: EF Core configuration + migration

**Scope:** `EasyBilling.Infrastructure`

- Add `DbSet<TimeEntry>`, `DbSet<ClientRate>`, `DbSet<EmailTemplate>`, `DbSet<InvoiceGenerationBatch>` to `AppDbContext`
- Fluent API config:
  - TimeEntry: composite unique index on `(CompanyId, ClientId, Date)`, cascade delete from Company and Client
  - ClientRate: composite unique index on `(ClientId, EffectiveFrom)`, cascade delete from Client
  - EmailTemplate: unique index on `ClientId` (one template per client), cascade delete from Client
  - InvoiceGenerationBatch → Invoice: one-to-many, optional FK (nullify on batch delete)
  - Invoice.Status: default value `InvoiceStatus.Approved` (backward compat)
- Generate migration: `AddBillableHoursEntities`

**Acceptance criteria:**
- `dotnet ef database update` succeeds on clean DB and on existing production schema
- Existing invoices get `Status = Approved` via default value
- All indexes created

---

### T-1.3: Repository interfaces + implementations

**Scope:** `EasyBilling.Application` (interfaces), `EasyBilling.Infrastructure` (implementations)

New repositories:

- **`ITimeEntryRepository`**
  - `GetByCompanyAndMonthAsync(Guid companyId, int month, int year)`
  - `GetByCompanyClientAndDateAsync(Guid companyId, Guid clientId, DateOnly date)` — for upsert
  - `UpsertAsync(TimeEntry entry)` — insert or update by composite key
  - `DeleteAsync(Guid id)`
  - `GetMonthlySummaryByClientAsync(Guid companyId, int month, int year)` — returns grouped totals

- **`IClientRateRepository`**
  - `GetByClientIdAsync(Guid clientId)` — all rates, ordered by EffectiveFrom desc
  - `GetEffectiveRateAsync(Guid clientId, DateOnly date)` — latest rate where EffectiveFrom <= date
  - `AddAsync(ClientRate rate)`
  - `DeleteAsync(Guid id)`

- **`IEmailTemplateRepository`**
  - `GetByClientIdAsync(Guid clientId)`
  - `UpsertAsync(EmailTemplate template)`
  - `DeleteAsync(Guid id)`

- **`IInvoiceGenerationBatchRepository`**
  - `GetByIdAsync(Guid id)` — includes related draft invoices
  - `GetByCompanyAsync(Guid companyId)` — list all batches
  - `AddAsync(InvoiceGenerationBatch batch)`
  - `UpdateStatusAsync(Guid id, BatchStatus status)`

Register all in `Program.cs`.

---

### T-1.4: Request models + DTOs

**Scope:** `EasyBilling.Application`

**Requests:**

- `UpsertTimeEntryRequest` — `CompanyId, ClientId, Date, Hours`
- `CreateClientRateRequest` — `ClientId, HourlyRate, Currency, EffectiveFrom`
- `UpsertEmailTemplateRequest` — `ClientId, Subject, Body, ToEmail, CcEmails, BccEmails`
- `GenerateInvoiceBatchRequest` — `CompanyId, Month, Year`
- `ApproveBatchRequest` — `BatchId`

**DTOs:**

- `TimeEntryDto` — `Id, ClientId, ClientName, Date, Hours`
- `MonthlyTimeSheetDto` — `CompanyId, Month, Year, Entries (List<TimeEntryDto>), ClientSummaries (List<ClientMonthlySummaryDto>)`
- `ClientMonthlySummaryDto` — `ClientId, ClientName, TotalHours, HourlyRate, Currency, TotalAmount`
- `ClientRateDto` — `Id, ClientId, HourlyRate, Currency, EffectiveFrom`
- `EmailTemplateDto` — mirrors entity
- `InvoiceGenerationBatchDto` — `Id, Month, Year, Status, GeneratedAt, InvoiceCount, TotalAmount`

---

## Phase 2 — Business Logic (Backend)

### T-2.1: TimeTrackingService

**Scope:** `EasyBilling.Application/Services`

- `UpsertTimeEntryAsync(UpsertTimeEntryRequest)` — validate hours (0 < h <= 24), validate total hours per day across all clients <= 24, upsert by (company, client, date)
- `DeleteTimeEntryAsync(Guid id)`
- `GetMonthlyTimesheetAsync(Guid companyId, int month, int year)` — returns full `MonthlyTimeSheetDto` with entries + per-client summaries (hours × effective rate = amount)
- Scoped to active company via `UserContext`

---

### T-2.2: ClientRateService

**Scope:** `EasyBilling.Application/Services`

- `SetRateAsync(CreateClientRateRequest)` — validate rate > 0, prevent duplicate EffectiveFrom for same client
- `GetRateHistoryAsync(Guid clientId)` — returns all rates ordered by date desc
- `GetEffectiveRateAsync(Guid clientId, DateOnly date)` — returns applicable rate; throws if no rate configured
- Scoped to active company via `UserContext`

---

### T-2.3: EmailTemplateService

**Scope:** `EasyBilling.Application/Services`

- `UpsertTemplateAsync(UpsertEmailTemplateRequest)` — validate email formats, required fields
- `GetTemplateAsync(Guid clientId)` — returns template or null
- `RenderTemplateAsync(Guid clientId, Invoice invoice)` — replaces placeholders: `{InvoiceNumber}`, `{InvoiceSeries}`, `{Month}`, `{Year}`, `{ClientName}`, `{TotalAmount}`, `{Currency}`, `{DueDate}`
- Scoped to active company via `UserContext`

---

### T-2.4: HolidayService

**Scope:** `IHolidayService` in Application, implementation in Infrastructure

- `GetHolidaysAsync(int year, string countryCode = "RO")` — calls both APIs:
  - `https://api.bank-holidays.ro` (primary)
  - `https://openholidaysapi.org/PublicHolidays?countryIsoCode=RO&validFrom={year}-01-01&validTo={year}-12-31`
- Compare results, log discrepancies, return union of both sets
- In-memory cache per year (holidays don't change mid-year)
- `GetLastWorkingDayAsync(int month, int year)` — calculates last business day excluding weekends + holidays
- `IsWorkingDayAsync(DateOnly date)` — helper

---

### T-2.5: InvoiceGenerationService

**Scope:** `EasyBilling.Application/Services`

- `GenerateDraftInvoicesAsync(Guid companyId, int month, int year)`:
  1. Fetch all TimeEntries for the month grouped by ClientId
  2. For each client: get effective rate, calculate total hours × rate
  3. Create draft Invoice (Status = Draft, no Series/Number yet) with one InvoiceLine: "Servicii consultanta {Month} {Year} — {TotalHours}h × {Rate}/{Currency}/h"
  4. Create InvoiceGenerationBatch linking all draft invoices
  5. Return batch summary

- `ApproveBatchAsync(Guid batchId)`:
  1. Validate all invoices in batch are Draft
  2. For each invoice: assign next Series/Number (call existing sequencing logic), set Status = Approved
  3. Update batch status to Approved
  4. Enqueue `InvoiceSendJob` for each invoice in the batch

- `RejectBatchAsync(Guid batchId)`:
  1. Set all draft invoices to Cancelled
  2. Set batch status to Failed

---

## Phase 3 — Notifications & Email (Backend)

### T-3.1: SMTP email service

**Scope:** `EasyBilling.Infrastructure/Services`

- `IEmailService` interface in Application: `SendEmailAsync(string to, string? cc, string? bcc, string subject, string body, byte[]? pdfAttachment, string? attachmentFilename)`
- Implementation: `SmtpEmailService` using `System.Net.Mail.SmtpClient` (or MailKit for more reliability)
- Config via env vars: `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM_EMAIL`
- Plain text body only (no HTML)
- Register in `Program.cs`

---

### T-3.2: SMSLink integration

**Scope:** `EasyBilling.Infrastructure/Services`

- `ISmsService` interface in Application: `SendSmsAsync(string phoneNumber, string message)`
- Implementation: `SmsLinkService` — HTTP POST to SMSLink.ro REST API
- Config via env var: `SMSLINK_API_KEY`
- Hardcoded SMS template: `"EasyBilling: {count} facturi generate pentru {monthName} {year}. Aproba: {approvalUrl}"`
- Register in `Program.cs`

---

### T-3.3: InvoiceSendJob (Hangfire)

**Scope:** `EasyBilling.Application/Jobs`

- Triggered after batch approval (one job per invoice in the batch)
- Steps:
  1. Generate PDF via existing `GenerateInvoicePdfAsync`
  2. Load EmailTemplate for the invoice's client
  3. Render template with invoice data
  4. Send email via `IEmailService` with PDF attachment
  5. Update Invoice.Status to `Sent`
  6. If `IsEFacturaActive` on company → optionally trigger ANAF submission
- Error handling: mark invoice as failed, don't block other invoices in batch

---

### T-3.4: MonthlyInvoiceGenerationJob (Hangfire recurring)

**Scope:** `EasyBilling.Application/Jobs`

- Recurring Hangfire job — runs daily at 08:00, checks if today is the last working day of the month
- If yes: for each company with time entries this month, call `InvoiceGenerationService.GenerateDraftInvoicesAsync`
- After generation: send SMS to admin user(s) of the company with approval link
- Register recurring job in `Program.cs`: `RecurringJob.AddOrUpdate<MonthlyInvoiceGenerationJob>(...)`

---

## Phase 4 — API Controllers (Backend)

### T-4.1: TimeTrackingController

**Scope:** `EasyBilling.Presentation/Controllers`

`[Authorize(Roles = "ADMIN")]`

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/time-entries?companyId&month&year` | Monthly timesheet with summaries |
| POST | `/api/time-entries` | Upsert time entry |
| DELETE | `/api/time-entries/{id}` | Delete time entry |

---

### T-4.2: ClientRateController

`[Authorize(Roles = "ADMIN")]`

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/client-rates?clientId` | Rate history for client |
| POST | `/api/client-rates` | Set new rate |
| DELETE | `/api/client-rates/{id}` | Remove rate |

---

### T-4.3: EmailTemplateController

`[Authorize(Roles = "ADMIN")]`

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/email-templates?clientId` | Get template for client |
| POST | `/api/email-templates` | Create/update template |
| DELETE | `/api/email-templates/{clientId}` | Remove template |
| POST | `/api/email-templates/preview` | Render template with sample data |

---

### T-4.4: InvoiceBatchController

`[Authorize(Roles = "ADMIN")]`

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/invoice-batches?companyId` | List batches |
| GET | `/api/invoice-batches/{id}` | Batch detail + draft invoices |
| POST | `/api/invoice-batches/generate` | Manual trigger for month |
| POST | `/api/invoice-batches/{id}/approve` | Approve & trigger send |
| POST | `/api/invoice-batches/{id}/reject` | Cancel all drafts in batch |

---

## Phase 5 — Frontend

### T-5.1: Types + API hooks

**Scope:** `types/`, `hooks/`

- `types/billableHours.ts` — TypeScript interfaces mirroring backend DTOs: `TimeEntry`, `MonthlyTimeSheet`, `ClientMonthlySummary`, `ClientRate`, `EmailTemplate`, `InvoiceGenerationBatch`
- `hooks/useBillableHours.ts` — React Query hooks: `useGetMonthlyTimesheet`, `useUpsertTimeEntry`, `useDeleteTimeEntry`
- `hooks/useClientRates.ts` — `useGetClientRates`, `useSetClientRate`
- `hooks/useEmailTemplates.ts` — `useGetEmailTemplate`, `useUpsertEmailTemplate`, `usePreviewEmailTemplate`
- `hooks/useInvoiceBatches.ts` — `useGetBatches`, `useGetBatchDetail`, `useGenerateBatch`, `useApproveBatch`, `useRejectBatch`

---

### T-5.2: Monthly calendar grid component

**Scope:** `components/BillableHoursCalendar.tsx`

- Monthly grid: 5 columns (Mon–Fri), rows per week
- Each day cell shows stacked client entries with hours
- Click cell → inline popover to add/edit hours per client (dropdown of company's clients + hours input)
- Color-coded per client (consistent colors)
- Weekly row totals + monthly column totals per client
- "Day off" visual indicator (grayed out, no entries)
- Navigation: month/year selector with arrows
- Responsive: grid on desktop, stacked cards on mobile

---

### T-5.3: Billable hours page

**Scope:** `app/billable-hours/page.tsx`

- Route: `/billable-hours?companyId={id}`
- Monthly calendar grid (T-5.2) as main content
- Sidebar or bottom panel: monthly summary table (client, hours, rate, amount, total)
- "Generate Invoices" button (manual trigger) — calls generate endpoint, shows confirmation
- Protected: redirect to /login if unauthenticated, admin-only check

---

### T-5.4: Client rate management UI

**Scope:** `components/ClientRateModal.tsx` or inline in billable hours page

- Per-client: set hourly rate + currency + effective date
- Rate history table (shows past rates)
- Accessible from billable hours page (icon/button next to client name in summary)

---

### T-5.5: Email template editor

**Scope:** `app/billable-hours/templates/page.tsx` + `components/EmailTemplateForm.tsx`

- Select client → edit subject/body/to/cc/bcc
- Placeholder buttons: click to insert `{InvoiceNumber}`, `{Month}`, etc.
- Preview button → calls preview endpoint, shows rendered output
- Simple form — textarea for body (plain text), inputs for email fields

---

### T-5.6: Batch review & approval page

**Scope:** `app/invoices/batch/[batchId]/review/page.tsx`

- This is the page linked from the SMS notification
- Shows all draft invoices in the batch: client, hours, rate, total, series (pending)
- "Approve & Send All" button — confirms, calls approve endpoint
- "Reject All" button — cancels batch
- Individual invoice preview (PDF) before approving
- Status indicators: Draft → Approved → Sent

---

### T-5.7: i18n translations

**Scope:** `messages/en.json`, `messages/ro.json`

- Add `billableHours`, `clientRates`, `emailTemplates`, `invoiceBatch` translation keys
- Both languages

---

## Phase 6 — Integration & Polish

### T-6.1: Update existing InvoiceService for Status lifecycle

- `CreateInvoiceAsync` — set Status to `Approved` by default (backward compat for manual creation)
- New `CreateDraftInvoiceAsync` — creates with Status = Draft, no Series/Number
- `ApproveInvoiceAsync` — assigns Series/Number, sets Status = Approved
- `GetAllInvoices` — add Status filter to `InvoicePaginationFilter`
- PDF generation: block for Draft status (no number assigned yet)

---

### T-6.2: End-to-end integration tests

**Scope:** `EasyBillings.Tests`

- Time entry CRUD + validation (hours limit, unique constraint)
- Rate lookup (effective date logic)
- Draft invoice generation from time entries
- Batch approve → invoice number assignment
- Email template rendering with placeholders
- Holiday service (mock both APIs, test comparison logic)

---

### T-6.3: Environment setup + deployment

- Add new env vars to VPS: `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM_EMAIL`, `SMSLINK_API_KEY`
- Update Azure DevOps pipeline if needed
- Update Nginx config if new routes need special handling
- Update CORS if needed
