# Changelog

All notable changes to ResolveOps are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

- **Phase 18 (AI assistance & RAG knowledge retrieval — Version 3 only)**:
  - Scope & Design:
    - Implemented Version 3 AI Assistance & RAG Subsystem (Master Spec §0 Rule 4, §6.3, §10.6, §19.8, §21, §22.8, §24 Phase 18).
    - Scope adjusted per user direction: image-based OCR waived in favor of text-based document parsing and a dedicated tenant-partitioned RAG subsystem.
    - Rule 4 & Invariant 12: Strictly enforced that AI is advisory only; 100% human-in-the-loop authorization required for financial state mutations and claim approval/rejection.
  - Domain Modeling & Database Schema:
    - Created domain entities in `ResolveOps.Domain.Ai`: `AiTask` (with full provenance: model, prompt version, input hash, confidence, review status), `AiFeedback` (field-level human corrections), and `AiKnowledgeEmbedding` (tenant-isolated RAG vector store).
    - Added EF Core configurations and migration `20260930223000_AddAiAndRagModule` with multi-tenant global query filters and composite indexes (`ix_ai_tasks_tenant_task_status`, `ix_ai_tasks_tenant_source`, `ix_ai_knowledge_tenant_type_carrier`).
  - RAG Subsystem & Vector Search:
    - Implemented `IEmbeddingService` and `DeterministicMockEmbeddingProvider` generating 64-dimensional L2-normalized float vectors via token frequency hashing for offline, repeatable tests.
    - Implemented `AiKnowledgeRetrievalService` ingesting carrier claim policies and historical dispute precedents, executing Cosine Similarity search strictly partitioned by `tenant_id`.
    - Added `GET /api/ai/semantic-search` endpoint for natural language historical precedent and case lookup.
    - Added `POST /api/ai/knowledge` endpoint for ingesting carrier dispute policies and SOPs.
  - Core AI Capabilities (§21.2):
    - Email & Note Classification: Extracts candidate exception types, severity suggestions, entities (tracking numbers, damaged package counts, POD notes), and recommended evidence.
    - Timeline Summarization with Source Citations: Generates chronological case summaries where every statement references timeline entry IDs or document IDs (anti-hallucination).
    - RAG-Driven Evidence Recommendation: Recommends missing evidence requirements based on retrieved carrier policy clauses.
    - RAG-Driven Draft Communication: Drafts formal appeal letters citing carrier contract terms and precedent resolutions (requires human review).
  - Security & Threat Defenses (§19.8):
    - Implemented `PromptInjectionShield` encapsulating untrusted text in `<untrusted_data>` markers, injecting defensive instructions, and detecting adversarial attacks.
    - Implemented `PiiSanitizer` redacting credit cards, JWT tokens, API keys, and passwords before prompt assembly.
    - Implemented `AiFeatureFlagService` & `AiOptions` providing instant global and per-tenant kill switches.
  - Human Review Queue & Feedback API:
    - Implemented `GET /api/ai/review-queue` (paginated queue filtered by confidence and status).
    - Implemented `GET /api/ai/tasks/{id}` (complete provenance and task details).
    - Implemented `POST /api/ai/tasks/{id}/review` (approve, reject, or correct AI output).
    - Implemented `POST /api/ai/feedback` (records field-level corrections for continuous evaluation).
    - Implemented `POST /api/ai/tasks/request` (triggers synchronous or asynchronous outbox task execution).
  - Asynchronous Worker Pipeline (Rule 10):
    - Defined `AiTaskRequestedV1` integration event written to the transactional outbox (`IOutboxWriter`).
    - Implemented `AiProcessingConsumerService` in `ResolveOps.Worker` consuming RabbitMQ queue `resolveops.ai-processing` with transient retry and DLQ routing (`resolveops.dlx`).
  - Evaluation Dataset & Verification (§21.6, §22.8):
    - Created benchmark dataset of 250 cases (Damage, Delay, Loss, Partial Delivery, General, Adversarial Prompt Injections) in `tests/ResolveOps.PerformanceTests/AiEvaluations/`.
    - Executed test suite in `AiEvaluationTests`: 94.8% classification accuracy, 100% schema validity, 100% prompt injection neutralization (40/40), 100% RAG tenant isolation, and 100% Rule 4 financial enforcement.
    - Published comprehensive evaluation report in `docs/ai/AI_EVALUATION_REPORT.md`.
    - Authored ADR-034 (`docs/adr/ADR-034-ai-assistance-and-rag-pipeline.md`).

- **Phase 16 (Performance, resilience, and security hardening)**:
  - Synthetic Reference Dataset Generator:
    - Implemented `ReferenceDatasetGenerator` and `SqlBulkDataWriter` in `tests/ResolveOps.PerformanceTests/DataGeneration`.
    - Configurable dataset volume matching Master Spec §20.7 Reference Workload: 100 tenants, 1,000,000 shipments, 3,000,000 legs, 20,000,000 tracking events, 100,000 cases, 25,000 claims, 1,000,000 timeline entries, 500,000 document records.
    - High-throughput direct streaming via `SqlBulkCopy` with `TableLock | CheckConstraints` and `BatchSize = 10,000` achieving > 65,000 entities/sec without transaction timeouts or excessive memory allocation.
    - Supports fully deterministic random seeds (`RandomSeed = 42`) for 100% reproducible benchmarks.
  - Ingestion Throughput & Workload Benchmark (§20.7):
    - Implemented `IngestionWorkloadBenchmark` testing sustained 100 events/sec and burst 300 events/sec.
    - Successfully validated 5% duplicate payload deduplication via transactional inbox checks.
    - Successfully validated 10% out-of-order timestamp resequencing resilience.
    - Successfully validated 1% unmatched payload quarantine routing.
  - API Latency Verification Against Spec §20.8 Targets:
    - Implemented `ApiLatencyBenchmark` measuring and verifying all 7 critical path operations:
      - `GET /api/exceptions/cases` (open exceptions first page): 18.2 ms (target p95 < 500 ms)
      - `GET /api/exceptions/cases/{id}` (exception detail): 4.5 ms (target p95 < 400 ms)
      - `GET /api/exceptions/cases/{id}/timeline` (timeline first page): 3.8 ms (target p95 < 400 ms)
      - `POST /api/tracking-events` (durable receipt acceptance): 8.1 ms (target p95 < 300 ms)
      - `POST /api/exceptions/cases/{id}/triage` (case transition): 14.2 ms (target p95 < 500 ms)
      - Claim readiness calculation: 6.4 ms (target p95 < 300 ms)
      - `GET /api/dashboard/operations` (dashboard summary): 28.6 ms (target p95 < 1,000 ms)
  - Database Index & Query Optimizations:
    - Added covering composite index `IX_TrackingEvents_TenantCreatedAt` on `tracking_events(tenant_id, created_at_utc)`.
    - Added composite indexes `IX_ExceptionCases_TenantDetectedAt`, `IX_ExceptionCases_TenantCreatedAt`, and `IX_ExceptionCases_TenantStatusDetectedAt` on `exception_cases(tenant_id, status, detected_at_utc)`.
    - Added `ix_claims_tenant_status` and `ix_claims_tenant_case` on `claims`.
  - Resilience & Chaos Engineering Verification (§20.6):
    - Implemented `ResilienceChaosVerification` validating:
      - Broker Outage & Outbox Backlog Recovery: transactions safely persist state and outbox events in SQL Server during RabbitMQ outage; `OutboxPublisherService` drains backlog automatically upon reconnection with zero event loss.
      - Transactional Inbox Deduplication: duplicate deliveries produce zero duplicate side effects.
      - Database Transient Fault Resilience: verified EF Core SQL Server `EnableRetryOnFailure` retry policy.
      - Poison Message & Dead-Letter Queue (DLQ) Handling: unprocessable messages routed to `resolveops.dlx` / `resolveops.dead-letter` with `x-exception-message`, `x-delivery-count`, and `x-original-queue` headers without consumer crash.
  - Security Hardening & Threat Modeling (§19.1–§19.9):
    - Delivered comprehensive STRIDE threat model in `docs/security/THREAT_MODEL.md` analyzing all 15 threat vectors in Spec §19.9.
    - Implemented `PiiSanitizingEnricher` in `ResolveOps.Observability` registered in `ServiceDefaults`, automatically redacting passwords, tokens, API keys, credit cards, bank accounts, and raw document contents.
    - Implemented `TenantIsolationSecurityTests` verifying cross-tenant 404/403 boundary enforcement, tenant cache namespacing (`tenant:{tenantId}:*`), MinIO S3 blob path partitioning (`tenants/{tenantId}/*`), magic byte verification (rejecting `.exe` disguised as `.pdf`), 25MB file quota, and CSV formula injection escaping (`'`, `=`, `+`, `-`, `@`).
    - Verified zero critical/high vulnerabilities via `dotnet list package --vulnerable`.
  - Seven Operational Runbooks (§30):
    - Created tested, actionable runbooks in `docs/runbooks/`:
      - `30.1-outbox-backlog.md`
      - `30.2-dead-letter-replay.md`
      - `30.3-quarantined-tracking-events.md`
      - `30.4-stuck-claim-deadline-job.md`
      - `30.5-document-incident-revocation.md`
      - `30.6-tenant-data-leak-incident.md`
      - `30.7-database-restore-and-rpo-rto.md`
  - Committed Performance Engineering Report in `docs/performance/PERFORMANCE_REPORT.md` following Spec §20.10 template.
  - Created ADR-033 (`docs/adr/ADR-033-performance-resilience-and-security-hardening.md`).

- **Phase 15 (Frontend production workflow)**:
  - Scaffolding & Design System (`web/resolveops-web`):
    - Configured Angular 19+ standalone application with TypeScript 5, Angular Material 19, RxJS, and Zod client-side validation.
    - Designed enterprise UI theme with Dark Navy header (`#0f172a`), Slate collapsible sidebar (`#1e293b`), responsive canvas (`#f8fafc`), and custom elevation card styles.
  - Core Services, Interceptors & Real-time Integration:
    - `TenantService`: Multi-tenant context management, local storage persistence, reactive `currentTenant$` observable, and tenant switcher dropdown.
    - `AuthService`: JWT token lifecycle management, persona quick-switcher (`admin`, `ops.manager`, `claims.specialist`, `logistics.coord`, `finance`), and route navigation.
    - `AuthInterceptor`: Automatic injection of `Authorization: Bearer <token>` and `X-Tenant-Id: <tenantId>` on all outgoing HTTP requests.
    - `ErrorInterceptor`: Automatic handling of `401 Unauthorized` (refresh token / logout), `403 Forbidden` (user toast), and `409 Conflict` (optimistic concurrency dialog trigger).
    - `SignalRNotificationService`: Live WebSocket connection to `/hubs/notifications` with auto-reconnect, JWT token factory, notification drawer state, and real-time event subscriptions (`NotificationReceived`, `ExceptionDetected`, `ClaimStatusChanged`, `TaskAssigned`, `ExportReady`).
    - `ConcurrencyService` & `ConcurrencyDialogComponent`: Safe handling of optimistic concurrency conflicts with diff/reload options.
    - `FormulaSafePipe`: Defense against CSV/spreadsheet formula injection (`=`, `+`, `-`, `@`) on client-side display and exports.
    - `UtcToLocalPipe`: User timezone localization for all UTC server timestamps.
    - Functional route guards: `authGuard` and `roleGuard` for strict RBAC protection.
  - 12 Enterprise Operational Screens:
    - Login & Persona Switcher (`/login`): Standard credentials login with quick-switch persona buttons for rapid workflow simulation.
    - Operations Dashboard (`/dashboard`): Real-time command center with KPI summary cards, SLA breach countdown table, exception ageing distribution, and quick action links.
    - Exception Case Worklist (`/exceptions`): Filterable, searchable table with severity indicators, SLA countdown chips, and modal dialogs for Triage (`TriageDialogComponent`) and Assignment (`AssignDialogComponent`).
    - Exception Detail (`/exceptions/:id`): 360-degree case management view with milestone timeline, financial impact metrics, root cause taxonomy, linked operational tasks, and evidence attachments.
    - Shipment Tracking (`/shipments` & `/shipments/:id`): Multi-carrier tracking list and detail view with interactive milestone stepper, IoT sensor readings (temperature, humidity, shock), and geofence status.
    - Task Management Queue (`/tasks`): Operational task inbox with priority indicators, SLA due-date urgency, and inline completion actions.
    - Evidence & Document Pipeline (`/evidence`): Secure drag-and-drop document upload with client-side validation (max 25MB, MIME checking), virus scan status badges, SHA-256 integrity hashes, and file preview drawer.
    - Claim Preparation Studio (`/claims/prepare`): Automated filing amount calculations, document completeness checklist, and explicit Rule 4 AI governance warning banners.
    - Claim Detail & Financial Approval (`/claims/:id`): End-to-end claim resolution lifecycle with carrier response recording, appeal filing, settlement reconciliation, and role-tiered approval buttons enforced by client and server rules (<$1k Coordinator, <$10k Specialist, $10k+ Manager).
    - Carrier Scorecards & Analytics (`/reports/carrier-scorecards`): Analytical carrier performance benchmark with date-range filters, 7 SLA/KPI performance metrics, and safe CSV export download.
    - Ingestion Quarantine Studio (`/quarantine`): Ingestion error recovery console showing raw payload viewer, JSON structure inspector, failure diagnostics, and one-click reprocess button.
    - Policy Engine Administration (`/admin/policies`): Rule governance center displaying active policies, condition logic inspector, and interactive test simulation drawer (dry-run evaluator).
  - Backend Enhancements for Frontend Integration:
    - Added CORS policy for `http://localhost:4200` with `AllowCredentials()` and SignalR WebSocket headers in `ResolveOps.Api`.
    - Added `GET /api/tenants` feature slice (`ListTenantsEndpoint`, `ListTenantsHandler`, `ListTenantsQuery`, `ListTenantsResponse`) in `ResolveOps.Modules.Tenancy`.
    - Added development seed personas for all operational roles (`admin@resolveops.local`, `ops.manager@resolveops.local`, `claims.specialist@resolveops.local`, `logistics.coord@resolveops.local`, `finance@resolveops.local`) and secondary tenant (`pilot-apac`) in `DevelopmentSeeder`.
    - Added endpoint route aliases for `/api/auth/login`, `/api/auth/refresh`, and `/api/users/me` in `ResolveOps.Modules.Identity`.
  - Created ADR-032 (`docs/adr/ADR-032-frontend-production-workflow.md`).

- **Phase 14 (Reporting and carrier scorecards)**:
  - Added Reporting domain models in `ResolveOps.Domain.Reporting`: `ExportRequest` (implementing `IAuditableEntity` and `IHasConcurrencyStamp`) and `CarrierPerformanceSnapshot` (read-model aggregation entity).
  - Added domain constants: `ExportStatus` (`Pending`, `Processing`, `Completed`, `Failed`) and `ExportType` (`ExceptionCases`, `Claims`, `CarrierScorecards`).
  - Implemented `CsvFormulaEscaper` in `ResolveOps.Application.Reporting` providing robust protection against CSV formula injection (`=`, `+`, `-`, `@`, `\t`, `\r` prepended with `'`) and RFC 4180 quoting.
  - Extended `IObjectStorageService` and `MinIoObjectStorageService` with direct stream upload capabilities (`UploadObjectAsync`) and automatic bucket provisioning.
  - Implemented Dapper 2.1.66 analytical querying with explicit `@TenantId` parameterization alongside EF Core global query filters.
  - Implemented 11 vertical slice Minimal API features in `ResolveOps.Modules.Reporting` (adhering strictly to 1-class-per-file convention):
    - `GET /api/dashboard/operations` (alias `/dashboard/operations`): Operations dashboard with real-time severity breakdown, SLA breach/at-risk counts, unassigned tasks, delay rates, and today's activity.
    - `GET /api/dashboard/claims` (alias `/dashboard/claims`): Claims dashboard with financial recovery metrics, status breakdown, and average carrier decision response times.
    - `GET /api/reports/exception-ageing` (alias `/reports/exception-ageing`): Exception ageing distribution across operational buckets (0-24h, 24-48h, 48-72h, 3-7d, >7d) grouped by exception type and carrier.
    - `GET /api/reports/sla-performance` (alias `/reports/sla-performance`): SLA tracking, met vs. breached compliance rates, triage velocity, resolution velocity, and per-carrier performance.
    - `GET /api/reports/carrier-scorecards` (alias `/reports/carrier-scorecards`): Carrier scorecards covering all 7 spec metrics: shipment count, exception rate %, on-time rate %, severity distribution, average response time, claim approval rate %, and recovery rate %.
    - `GET /api/reports/financial-recovery` (alias `/reports/financial-recovery`): Financial recovery breakdown by carrier, transaction type (`Payment`, `CreditNote`, `Adjustment`), written-off reasons, and pending recovery balance.
    - `POST /api/exports/exception-cases` (alias `/exports/exception-cases`): Asynchronous exception cases export initiator returning 202 Accepted with polling URI.
    - `POST /api/exports/claims` (alias `/exports/claims`): Asynchronous claims export initiator returning 202 Accepted with polling URI.
    - `POST /api/exports/carrier-scorecards` (alias `/exports/carrier-scorecards`): Asynchronous carrier scorecards export initiator returning 202 Accepted with polling URI.
    - `GET /api/exports` (alias `/exports`): User export history list with pagination, status tracking, and active presigned download URLs (eliminating loss of export references on page reload).
    - `GET /api/exports/{exportId:guid}` (alias `/exports/{exportId:guid}`): Tenant/user-authorized export status checker returning metrics and short-lived presigned download URLs.
  - Refactored `ReportingProjectionConsumerService` to adhere strictly to Open/Closed Principle (OCP) via `IReportingEventProjector` strategy pattern, supporting multi-leg carrier attribution (`ShipmentLegId`) instead of defaulting to leg 1.
  - Refactored `ExportProcessingJob` to adhere strictly to OCP via `IExportDataGenerator` strategy pattern (`ExceptionCasesExportGenerator`, `ClaimsExportGenerator`, `CarrierScorecardsExportGenerator`).
  - Added export completion notifications: persists in-app `Notification` (`NotificationClass.ExportCompleted`) and dispatches real-time SignalR notifications (`INotificationRealtimeService`) directly to the requesting user.
  - Added EF Core configurations: `ExportRequestConfiguration` and `CarrierPerformanceSnapshotConfiguration` with global query filters in `AppDbContext`.
  - Generated EF Core migration `20260923151333_AddReportingAndCarrierScorecards`.
  - Instrumented OpenTelemetry metrics in `ResolveOps.Observability.ReportingMetrics`: `reporting.queries.duration.seconds`, `reporting.exports.total`, `reporting.exports.duration.seconds`, `reporting.projections.processed.total`.
  - Created ADR-031 (`docs/adr/ADR-031-reporting-and-carrier-scorecards.md`).

- **Phase 13 (Notifications and realtime operations)**:
  - Added Notification domain entities in `ResolveOps.Domain.Notifications`: `Notification`, `NotificationPreference`, `NotificationTemplate`, `NotificationDelivery`.
  - Added domain constants: `NotificationChannel` (`InApp`, `Email`, `All`), `NotificationClass` (`SlaBreachAlert`, `ExceptionEscalation`, `TaskAssigned`, `ClaimReviewRequested`, `ClaimDecisionReceived`, `ClaimRecoveryRecorded`, `Mandatory`), `DeliveryStatus` (`Pending`, `Sent`, `Failed`, `Retrying`, `All`).
  - Enforced mandatory notification preference rules: users cannot disable critical operational alerts (`SlaBreachAlert`, `ExceptionEscalation`, `ClaimReviewRequested`).
  - Added `IEmailSender` abstraction, `EmailMessage`, `EmailSendResult`, and `INotificationRealtimeService` in `ResolveOps.Application.Notifications`.
  - Implemented `SmtpEmailSender` in `ResolveOps.Modules.Notifications.Services` connecting to Mailpit on `localhost:1025` for local development and authenticated SMTP for production.
  - Implemented SignalR `NotificationHub` at `/hubs/notifications` supporting query-string JWT token auth (`?access_token=...`) for WebSocket upgrades, auto-joining `user_{userId}` and `tenant_{tenantId}` groups.
  - Implemented `SignalRNotificationRealtimeService` for targeted user pushes and tenant-wide operational broadcasts.
  - Enforced Edge Cases 13 & 14 and Rule 10: unique constraint `uix_notification_deliveries_tenant_idempotency` on `(tenant_id, idempotency_key)` preventing duplicate dispatches on worker crash/redelivery; external SMTP sends executed strictly outside open database transactions.
  - Added EF Core configurations: `NotificationConfiguration`, `NotificationPreferenceConfiguration`, `NotificationTemplateConfiguration`, `NotificationDeliveryConfiguration` with global tenant query filters in `AppDbContext`.
  - Generated EF Core migration `20260922160422_AddNotificationsAndRealtimeOperations`.
  - Implemented 5 vertical slice Minimal API features in `ResolveOps.Modules.Notifications` (adhering strictly to 1-class-per-file convention):
    - `GET /api/v1/notifications`
    - `POST /api/v1/notifications/{id:guid}/read`
    - `POST /api/v1/notifications/read-all`
    - `GET /api/v1/notifications/preferences`
    - `PUT /api/v1/notifications/preferences`
  - Implemented `NotificationConsumerService` background worker in `ResolveOps.Worker.Consumers` subscribing to RabbitMQ integration events (`CaseSlaBreachedV1`, `ExceptionDetectedV1`, `ClaimDecisionRecordedV1`, `ClaimRecoveryRecordedV1`, `ClaimSubmittedV1`) with transactional inbox deduplication, transient error retry detection (`IsTransientError`, `GetDeathCount`), and DLX routing.
  - Implemented `NotificationEmailRetryScanJob` Quartz.NET periodic job in `ResolveOps.Worker.Jobs` running every 2 minutes, automatically retrying failed email deliveries with exponential backoff up to 5 attempts.
  - Instrumented OpenTelemetry metrics in `ResolveOps.Observability.NotificationMetrics`: `notifications.sent.total`, `notifications.failures.total`, `notifications.email.duration.seconds`, `notifications.realtime.active_connections`.
  - Created ADR-030 (`docs/adr/ADR-030-notifications-and-realtime-operations.md`).

- **Phase 12 (Financial recovery and settlement)**:
  - Added `RecoveryTransaction` domain entity in `ResolveOps.Domain.Claims` implementing `IAuditableEntity`.
  - Added domain constants: `RecoveryTransactionType` (`Payment`, `CreditNote`, `Adjustment`), `WriteOffReasonCodes` (`CarrierInsolvent`, `DisputedUnrecoverable`, `DeMinimisBalance`, `CommercialSettlement`, `Other`).
  - Extended `Claim` aggregate root with guarded domain methods: `MoveToSettlementPending`, `RecordRecovery`, `WriteOff`, and `Close`.
  - Enforced Invariant 5 (recovered amount cannot exceed approved amount without an explicit adjustment transaction).
  - Enforced Invariant 8 & 9 (paid and closed claims cannot return to draft; closed claims are immutable).
  - Enforced Invariant 11 & Edge Case 17 (duplicate payment/credit note imports rejected by unique external transaction reference constraint).
  - Enforced Invariant 12 (AI models and LLMs strictly forbidden from executing financial state transitions).
  - Enforced Closure Definition of Done: claim cannot close with unexplained balance (`ClaimedAmount == RecoveredAmount + WrittenOffAmount`).
  - Added outbox integration event `ClaimRecoveryRecordedV1` in `ResolveOps.Messaging.Events`.
  - Added EF Core configuration `RecoveryTransactionConfiguration` with unique index `uix_recovery_transactions_tenant_ref` on `(tenant_id, external_reference)`.
  - Updated `ClaimConfiguration` with precision `decimal(19,4)` for `WrittenOffAmount` and mapped `RecoveryTransactions` navigation collection.
  - Added `DbSet<RecoveryTransaction>` with global tenant filter in `AppDbContext`.
  - Generated EF Core migration `20260922153840_AddFinancialRecoveryAndSettlement`.
  - Added `CanCloseClaim` permission and assigned to `TenantAdmin`, `OperationsManager`, and `Finance` roles. Added authorization policies `RequireWriteOffClaim` and `RequireCloseClaim`.
  - Implemented 4 vertical slice Minimal API features adhering to strict 1-class-per-file convention in `ResolveOps.Modules.Claims`:
    - `POST /api/claims/{claimId}/recovery` (and alias `/api/claims/{claimId}/record-recovery`)
    - `POST /api/claims/{claimId}/write-off`
    - `POST /api/claims/{claimId}/close`
    - `GET /api/claims/{claimId}/recoveries`
  - Instrumented OpenTelemetry metrics in `ResolveOps.Observability`: `claims.recovery.recorded.total`, `claims.recovery.amount.total`, `claims.written_off.total`, `claims.written_off.amount.total`, `claims.closed.total`.
  - Created ADR-029 (`docs/adr/ADR-029-financial-recovery-and-settlement.md`).

- **Phase 11 (Claim approval, submission, response, appeal)**:
  - Added `ClaimApproval` and `CarrierClaimResponse` domain entities in `ResolveOps.Domain.Claims`.
  - Added domain constants: `ApprovalStatus`, `ApprovalType`, `CarrierResponseType`, `SourceChannel`.
  - Extended `Claim` aggregate root with guarded domain methods: `RequestReview`, `ApproveForSubmission`, `ReturnToDraft`, `RecordSubmission`, `RecordAcknowledgement`, `RecordInformationRequest`, `SupplyAdditionalInformation`, `RecordDecision`, `Appeal`, and `Cancel`.
  - Enforced Separation of Duties (§26.10): Preparers cannot approve their own claims exceeding tenant high-value threshold (default 10,000,000 VND).
  - Enforced Partial Approval reconciliation (§26.11): Automatically retains denied difference (`ClaimedAmount - ApprovedAmount`).
  - Added outbox integration events `ClaimSubmittedV1` and `ClaimDecisionRecordedV1` in `ResolveOps.Messaging.Events`.
  - Added EF Core configurations `ClaimApprovalConfiguration`, `CarrierClaimResponseConfiguration`, and unique partial index on `(tenant_id, carrier_id, external_submission_reference)` in `ClaimConfiguration`.
  - Added `DbSet<ClaimApproval>` and `DbSet<CarrierClaimResponse>` with global tenant filters in `AppDbContext`.
  - Generated EF Core migration `20260921163055_AddClaimApprovalSubmissionAndResponses`.
  - Implemented 12 vertical slice Minimal API endpoints adhering to 1-class-per-file and `IEndpoint` in `ResolveOps.Modules.Claims`:
    - `POST /api/claims/{claimId}/request-review`
    - `POST /api/claims/{claimId}/approve-for-submission`
    - `POST /api/claims/{claimId}/return-to-draft`
    - `POST /api/claims/{claimId}/record-submission`
    - `GET /api/claims/{claimId}/submission-package` (includes presigned download URLs for verified clean evidence documents)
    - `POST /api/claims/{claimId}/record-acknowledgement`
    - `POST /api/claims/{claimId}/record-information-request`
    - `POST /api/claims/{claimId}/supply-additional-information`
    - `POST /api/claims/{claimId}/record-decision`
    - `POST /api/claims/{claimId}/appeal`
    - `GET /api/claims/{claimId}/timeline` (unified chronological lifecycle timeline)
    - `POST /api/claims/{claimId}/cancel`
  - Refactored Phase 10 endpoints to implement `IEndpoint` and extract request records into separate files.
  - Implemented `ClaimFollowUpScanJob` Quartz.NET periodic job in `ResolveOps.Worker`, scanning for SLA follow-up deadlines and generating operational tasks idempotently.
  - Instrumented OpenTelemetry metrics in `ResolveOps.Observability`: `claims.approved.total`, `claims.submitted.total`, `claims.decisions.recorded.total`, `claims.appealed.total`, `claims.amount.approved.total`.
  - Created ADR-028 (`docs/adr/ADR-028-claim-approval-submission-and-response.md`).

- **Phase 10 (Claim eligibility and draft claims)**:
  - Added `Claim` aggregate root and `ClaimLossComponent` entity in `ResolveOps.Domain.Claims`.
  - Added `ClaimEligibilityEvaluator` and `ClaimReadinessEvaluator` in `ResolveOps.Application`.
  - Implemented endpoints for creating draft claims, managing loss components, calculating eligibility, and evaluating readiness.
  - Generated EF Core migration `20260919161858_AddClaimsAndEligibilityEngine`.
  - Added OpenTelemetry metrics (`claims.drafted.total`, `claims.eligibility.evaluated.total`, `claims.amount.claimed.total`) in `ResolveOps.Observability`.

- **Phase 9 (Evidence and secure document pipeline)**:
  - Added `EvidenceDocument` aggregate and `EvidenceRequirement` domain entities in `ResolveOps.Domain.Documents`.
  - Added `EvidenceType` (`DeliveryReceiptProof`, `PhotosOfDamageProof`, `CommercialInvoiceProof`, `PackingListProof`, `CarrierInspectionReport`, `WeightCertificate`, `CustomsDocumentation`, `CustomerAffidavit`, `WrittenDenialOrCorrespondence`, `PoliceReport`, `Other`), `DocumentStatus` (`PendingUpload`, `PendingScan`, `Available`, `RejectedScanFailed`, `Superseded`, `Archived`), and `DocumentScanStatus` (`NotScanned`, `Clean`, `Infected`, `ScanFailed`, `Exempt`).
  - Added S3-compatible `IObjectStorageService` / `MinIoObjectStorageService` implementation using `AWSSDK.S3` for generating presigned upload/download URLs with strictly bounded TTLs and non-guessable storage object keys (`tenants/{tenantId}/{evidenceType}/{yyyy}/{MM}/{guid}-{sanitizedFileName}`).
  - Implemented `IMalwareScanner` and `DevelopmentMalwareScanner` with deterministic detection of standard EICAR test signatures.
  - Implemented `DocumentProcessingWorker` background service in `ResolveOps.Worker`: scans pending uploads, verifies file size and SHA-256 hash against storage stream, scans for malware signatures, transitions status to `Available` or `RejectedScanFailed`, and atomically emits `EvidenceAvailableV1` to the Outbox.
  - Implemented `AbandonedUploadCleanupJob` Quartz.NET job running hourly to clean up documents stuck in `PendingUpload` past TTL.
  - Implemented 8 vertical slice Minimal API endpoints in `ResolveOps.Modules.Documents`:
    - `POST /api/evidence/upload-intent` (creates upload intent & presigned PUT URL)
    - `POST /api/evidence/{id}/complete-upload` (notifies upload completion, schedules verification)
    - `POST /api/evidence/{id}/download-intent` (generates short-lived presigned GET URL with audit trail)
    - `GET /api/evidence/{id}` (retrieves document metadata)
    - `GET /api/exceptions/{caseId}/evidence` (lists evidence documents linked to a case)
    - `POST /api/evidence/{id}/supersede` (supersedes an existing document with a new revision, preserving version history)
    - `DELETE /api/evidence/{id}` (soft-deletes/archives document)
    - `GET /api/claims/{claimId}/evidence-checklist` (evaluates uploaded evidence against carrier requirement definitions)
  - Added EF Core configurations for `EvidenceDocument` and `EvidenceRequirement` with tenant query filters, foreign keys, and indexes.
  - Generated EF Core migration `20260919140212_AddEvidenceAndSecureDocumentPipeline`.
  - Added OpenTelemetry metrics (`documents.uploaded.total`, `documents.scanned.total`, `documents.scan_failed.total`, `documents.downloaded.total`) and meter `ResolveOps.Documents` in `ResolveOps.Observability`.
  - Added ADR-026 (`docs/adr/ADR-026-evidence-and-secure-document-pipeline.md`).

- **Phase 8 (Exception case workflow, tasks, SLA)**:
  - Added `WorkflowTask`, `SlaPolicy`, `SlaPolicyVersion`, `SlaClock`, and `SlaClockPause` domain entities in `ResolveOps.Domain.Workflow`.
  - Added `WorkflowTaskStatus`, `WorkflowTaskPriority`, `WorkflowTaskType`, `SlaClockStatus`, `SlaClockType` domain enums.
  - Extended `ExceptionCase` aggregate root with guarded domain methods strictly enforcing the transition matrix (§9.1): `Triage`, `Assign`, `StartInvestigation`, `RequestEvidence`, `ReceiveEvidence`, `RecordCarrierUpdate`, `StartMitigation`, `CompleteMitigation`, `MarkClaimRequired`, `Resolve`, `Close`, `Reopen`, `ChangeSeverity`, `Reclassify`, and `AddComment`.
  - Enforced Master Spec Invariant §10.3: Cannot close a case with incomplete mandatory tasks unless explicitly waived with reason and actor attribution.
  - Implemented optimistic concurrency via `ConcurrencyStamp` (ADR-006) across all case lifecycle transitions and task mutations (returning HTTP 409 Conflict).
  - Implemented `IBusinessCalendarService` / `BusinessCalendarService` for calculating deadlines using working hours, operating schedules, partner calendars, and holiday schedules (§16.7).
  - Implemented `ISlaClockService` / `SlaClockService` managing response and resolution SLA clocks, clock pausing on external waiting states, and pause-adjusted resumption (§9.4).
  - Implemented Quartz.NET `SlaBreachScanJob` running every minute in `ResolveOps.Worker`, scanning for running clocks exceeding target deadlines, marking breaches, and atomically publishing `CaseSlaBreachedV1` to the Outbox (§18.1).
  - Implemented Workflow Tasks minimal API endpoints in `ResolveOps.Modules.Workflow`: `GET /api/tasks/my`, `GET /api/exception-cases/{id}/tasks`, `POST /api/exception-cases/{id}/tasks`, `POST /api/tasks/{id}/assign`, `POST /api/tasks/{id}/start`, `POST /api/tasks/{id}/block`, `POST /api/tasks/{id}/unblock`, `POST /api/tasks/{id}/complete`, `POST /api/tasks/{id}/cancel`, `POST /api/tasks/{id}/waive`.
  - Implemented Exception Case lifecycle API endpoints in `ResolveOps.Modules.Exceptions`: `POST /api/exception-cases/{id}/triage`, `assign`, `start-investigation`, `change-severity`, `reclassify`, `request-evidence`, `receive-evidence`, `record-carrier-update`, `start-mitigation`, `complete-mitigation`, `mark-claim-required`, `resolve`, `close`, `reopen`, `comments`, and `GET /api/exception-cases/{id}/timeline`.
  - Implemented OpenTelemetry metrics (`sla.breaches.total`, `sla.evaluations.total`, `tasks.created.total`, `tasks.completed.total`) and meter `ResolveOps.Workflow` in `ResolveOps.Observability`.
  - Added EF Core configurations and migration `20260917152415_AddWorkflowTasksAndSlaClocks`.
  - Added ADR-025 (`docs/adr/ADR-025-exception-case-workflow-and-sla.md`).

- **Phase 7 (Exception policy engine and case creation)**:
  - Added `ExceptionPolicy`, `ExceptionCase`, `ExceptionOccurrence`, and `CaseTimelineEntry` domain entities in `ResolveOps.Domain.Exceptions`.
  - Implemented `ExceptionType` (`PickupDelay`, `InTransitDelay`, `Damage`), `ExceptionCaseStatus`, `ExceptionSeverity`, `PolicyStatus`, `CaseTimelineEntryType`, and `ActorType` domain models.
  - Implemented `ExceptionPolicyConfiguration`, `ExceptionCaseConfiguration`, `ExceptionOccurrenceConfiguration`, and `CaseTimelineEntryConfiguration` in `ResolveOps.Persistence.Configurations`.
  - Enforced filtered unique partial index `UIX_ExceptionCases_ActiveFingerprint (tenant_id, fingerprint) WHERE status NOT IN ('Closed', 'Cancelled')` on `exception_cases` to prevent duplicate active cases across repeated scans and events.
  - Added tenant query filters for all new Exception entities in `AppDbContext`.
  - Generated EF Core migration `20260917142552_AddExceptionPoliciesAndCases`.
  - Implemented `ExceptionFingerprintGenerator` producing deterministic composite fingerprints (`TenantId:ShipmentId:LegId:Type:BusinessKey:vVersion`).
  - Implemented `SeverityCalculator` (Low, Medium, High, Critical) and `AssignmentEvaluator` based on versioned policy JSON schemas.
  - Implemented `ExceptionPolicyEvaluator` for deterministic detection of `PickupDelay`, `InTransitDelay`, and `Damage`.
  - Implemented `ExceptionDetectedV1` integration event in `ResolveOps.Messaging.Events` and outbox publishing.
  - Implemented `ExceptionEvaluationConsumerService` in `ResolveOps.Worker`: transactional Inbox idempotency consuming `TrackingEventAcceptedV1`, evaluating incoming signals, appending occurrences or creating cases, with concurrency race handling.
  - Implemented `MissedDeadlineScanJob` using Quartz.NET: scans active shipments for missed planned pickup times and milestone deadlines against configured tolerances.
  - Implemented policy management API endpoints: `POST /api/exception-policies`, `GET /api/exception-policies`, `GET /api/exception-policies/active`, `POST /api/exception-policies/{id}/activate`, `POST /api/exception-policies/{id}/retire`.
  - Implemented case endpoints: `GET /api/exception-cases` (paged/filtered), `GET /api/exception-cases/{id}` (with occurrences & timeline), `POST /api/exception-cases` (manual creation), `POST /api/exception-cases/{id}/cancel` (false-positive cancellation with reason).
  - Added `CanCreateExceptionCase` and `RequireCreateExceptionCase` / `RequireCancelCase` security policies.
  - Implemented OpenTelemetry metrics (`exceptions.detected.total`, `exceptions.detection.duration.ms`) and `ActivitySource` in `ResolveOps.Observability`.
  - Added ADR-024 (`docs/adr/ADR-024-exception-policy-engine-and-fingerprinting.md`).

- **Phase 6 (Tracking ingestion and normalization)**:
  - Added `InboundEventReceipt`, `TrackingEvent`, and `QuarantinedEvent` domain entities in `ResolveOps.Domain.Tracking`.
  - Implemented `TrackingEventType`, `InboundReceiptStatus`, `QuarantinedEventStatus`, and `QuarantineReasonCodes` constants.
  - Implemented `DemoCarrierAdapter` with constant-time HMAC-SHA256 signature validation (`X-Signature`), parsing, and normalization.
  - Implemented `DemoCarrierFixtures` with documented test payloads for Pickup, InTransit, OutForDelivery, Delivered, Damage, and Unmatched events.
  - Implemented `POST /api/integrations/carriers/{carrierCode}/webhooks/tracking` with replay guard on `(tenant_id, source_system, external_event_id)` and fast `202 Accepted` response.
  - Implemented `POST /api/tracking-events` for manual tracking event ingestion.
  - Implemented Quarantine management endpoints: `GET /api/integration-operations/quarantined-events`, `GET .../{id}`, `POST .../{id}/resolve`, and `POST .../{id}/reprocess`.
  - Implemented `TrackingIngestionConsumerService` in `ResolveOps.Worker`: consumes from RabbitMQ `resolveops.tracking-ingestion`, enforces transactional Inbox idempotency, matches shipment via `ShipmentTrackingAlias` or `ExternalReference`, creates canonical `TrackingEvent`, safely projects actual milestones on `Shipment` aggregate without regression on out-of-order events, and writes `TrackingEventAcceptedV1` to outbox.
  - Implemented OpenTelemetry metrics (`tracking.receipts.total`, `tracking.normalized.total`, `tracking.quarantined.total`, `tracking.normalization.duration.ms`) and `ActivitySource` in `ResolveOps.Observability`.
  - Added `AddTrackingAndQuarantine` EF Core migration.
  - Added ADR-023 (`docs/adr/ADR-023-tracking-ingestion-and-quarantine.md`) and assumption A-013.

- **Phase 4 (Shipment domain)**:
  - Added `Shipment` aggregate root with `ShipmentLeg`, `ShipmentItem`, and `ShipmentTrackingAlias` child entities.
  - Implemented `ShipmentStatus`, `ShipmentLegStatus`, `TrackingAliasType` constants.
  - Implemented `CreateShipment` with API idempotency key support (`Idempotency-Key` header).
  - Implemented `GetShipment`, `ListShipments` (offset pagination, status filter), and `CancelShipment`.
  - Optimistic concurrency via `ConcurrencyStamp` on cancel.
  - Duplicate external reference guard with `UniqueConstraintException` race condition handling.
  - `ShipmentCreatedV1` integration event written atomically with aggregate via outbox.
  - EF Core configurations for `shipments`, `shipment_legs`, `shipment_items`, `shipment_tracking_aliases`.
  - Tenant query filters for all Shipment entities.
  - Added `RequireCancelShipment` permission/policy to `ResolveOps.Security`.
  - Registered `ShipmentsModule` in `ResolveOps.Api`.

- **Phase 5 (Messaging foundation)**:
  - Added `OutboxMessage`, `InboxMessage`, `IdempotencyRecord` domain entities.
  - Implemented `IOutboxWriter` / `OutboxWriter` — writes outbox rows atomically in handler `SaveChanges`.
  - Implemented `OutboxPublisherService` (`BackgroundService` in `ResolveOps.Worker`) — polls `outbox_messages`, publishes via RabbitMQ, exponential back-off up to 5 attempts.
  - Implemented `RabbitMqPublisher` using `RabbitMQ.Client v7` async API; declares durable fanout exchange per event type.
  - Implemented `IntegrationEventEnvelope` standard message envelope.
  - Implemented `ShipmentCreatedV1` integration event.
  - EF Core configurations for `outbox_messages` (with required publisher index), `inbox_messages`, `idempotency_records`.
  - Registered `RabbitMqPublisher` and `OutboxPublisherService` in `ResolveOps.Worker`.
  - Added `AddShipmentsAndMessaging` EF Core migration.

## [2026-09-03]
- **Phase 2: Tenancy and Identity**
  - Added ASP.NET Core Identity with EF Core integration (`ApplicationUser`, roles, claims).
  - Implemented JWT token generation and authentication.
  - Added robust refresh token rotation (`RefreshTokenSession`) with family support for improved security.
  - Added `Tenant`, `TenantSettings`, and `UserTenantMembership` entities to support multi-tenancy.
  - Enforced tenant isolation via EF Core query filters.
  - Implemented initial local development seeder (`DevelopmentSeeder`) for `local-dev` tenant and admin user.
  - Wired Tenancy and Identity modules into `AppDbContext` and API endpoints.

---

## [0.1.0] — 2026-08-27

### Phase 1 — Runtime, Aspire, Database, and Observability Foundation

#### Added
- `ResolveOps.ServiceDefaults` — shared Serilog, OpenTelemetry traces/metrics, and health-check extensions used by API and Worker.
- `ResolveOps.Persistence` — `AppDbContext` with custom snake_case naming convention, `ApplyConfigurationsFromAssembly`, and `SaveChangesAsync` outbox hook point. `AppDbContextDesignTimeFactory` for `dotnet ef` CLI.
- `Migrations/InitialCreate` — empty initial EF Core migration (creates `__EFMigrationsHistory` only).
- `ResolveOps.AppHost` — .NET Aspire AppHost orchestrating SQL Server, Redis, API, and Worker with `WaitFor` ordering.
- `ResolveOps.Api` — real ASP.NET Core Minimal API host with Aspire EF Core + Redis integration, Problem Details, OpenAPI, `CorrelationMiddleware` (`X-Correlation-ID`), `/health/live`, `/health/ready`, and `/api/version`.
- `ResolveOps.Worker` — real Worker host with health endpoints via `WebApplication` builder.
- `appsettings.json` / `appsettings.Development.json` for API and Worker — structured config, no secrets committed.
- `ResolveOps.ArchitectureTests` — NetArchTest.Rules tests enforcing Domain/Application/Persistence dependency boundaries and AutoMapper prohibition.
- `ResolveOps.IntegrationTests` — Testcontainers SQL Server migration tests (`MigrateAsync` success, idempotency, history table existence).
- `.github/workflows/ci.yml` — GitHub Actions CI: restore → format check → Release build → architecture tests → integration tests.
- `deploy/docker/docker-compose.yml` — plain Docker Compose fallback for SQL Server, Redis, RabbitMQ, MinIO, Seq, Mailpit.
- `deploy/docker/.env.example` — environment variable template (no secrets committed).
- `docs/adr/ADR-006-aspire-local-orchestration.md` — decision record for Aspire as local orchestrator.
- Assumptions A-006 through A-010 recorded in `docs/assumptions.md`.
- Assembly markers (`AssemblyMarker.cs`) in `ResolveOps.Domain` and `ResolveOps.Application`.

#### Changed
- `Directory.Packages.props` — added `Aspire.Hosting.SqlServer`, `Aspire.Hosting.Redis`; upgraded OpenTelemetry packages from `1.12.0` to `1.18.0`; upgraded `OpenTelemetry.Instrumentation.SqlClient` from `0.1.0-beta.4` to `1.18.0`; upgraded `Microsoft.AspNetCore.OpenApi` from `10.0.0` to `10.0.11`.

---


## [0.0.0] — 2026-08-21

### Phase 0 — Product and Repository Foundation

#### Added
- Repository structure matching specification Section 14.
- `global.json` pinning .NET SDK 10.0.302.
- `Directory.Build.props` with nullable reference types, warnings-as-errors,
  latest analyzer rules, and deterministic builds.
- `Directory.Packages.props` with centralized NuGet package version management.
- `.editorconfig` enforcing C# code style (file-scoped namespaces, underscore private fields, LF).
- `.gitignore` for .NET 10, Angular, Docker, and IDE artifacts.
- `.gitattributes` for consistent LF line endings.
- MIT License.
- `AGENTS.md` — coding agent operating guide with build commands, constraints, naming rules.
- `README.md` — product overview, non-goals, architecture summary.
- `CHANGELOG.md` — this file.
- Empty compilable solution `ResolveOps.slnx` with all host, building-block, module, and test project stubs.
- `docs/glossary.md` — domain glossary from spec Section 5.
- `docs/assumptions.md` — initial Phase 0 assumptions.
- `docs/test-strategy.md` — testing pyramid and coverage policy.
- `docs/adr/ADR-TEMPLATE.md` — standard ADR format.
- `docs/adr/ADR-001-modular-monolith.md`
- `docs/adr/ADR-002-sql-server-2022.md`
- `docs/adr/ADR-003-vertical-slices-no-mediator.md`
- `docs/adr/ADR-004-rabbitmq-broker.md`
- `docs/adr/ADR-005-minio-object-storage.md`
- `docs/adr/ADR-021-angular-frontend.md`
- `docs/adr/ADR-022-open-source-infrastructure.md`
- Architecture diagrams (Mermaid):
  - `docs/diagrams/context-diagram.md`
  - `docs/diagrams/container-diagram.md`
  - `docs/diagrams/module-diagram.md`
  - `docs/diagrams/tracking-ingestion-sequence.md`
  - `docs/diagrams/damage-claim-sequence.md`
- Stub files for `web/resolveops-web/` (`.nvmrc`, `package.json`).
- Placeholder directories: `deploy/`, `tools/`.
