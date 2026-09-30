# ADR-034 — AI Assistance, Human Review Queue, and RAG Pipeline Architecture

**Status:** Accepted  
**Date:** 2026-09-30  
**Phase:** 18 (Prioritized before Phase 17)  
**Authors:** Coding Agent  

---

## Context

Phase 18 implements the Version 3 Artificial Intelligence assistance and Retrieval-Augmented Generation (RAG) capabilities for ResolveOps (Master Specification §0 Rule 4, §4.1/4.2, §6.3, §10.6, §19.8, §21, §22.8, §24 Phase 18, §25).

As a platform managing high-stakes logistics exceptions and financial carrier claims, integrating LLM capabilities introduces critical architectural and security constraints:
1. **Rule 4 & Invariant 12 (Non-negotiable):** Under no circumstance may an AI or LLM approve, reject, pay, settle, or execute financial or aggregate state transitions on claims. AI is strictly advisory.
2. **Untrusted Data & Prompt Injection Boundary (§19.8):** External customer emails, carrier notes, and document text are untrusted user inputs. Adversaries may attempt indirect prompt injection to hijack instructions, bypass security gates, or extract system secrets.
3. **Multi-Tenant Retrieval Boundary (§19.8):** In a multi-tenant platform, vector embeddings, semantic search, and knowledge retrieval must be physically isolated by `tenant_id`. Tenant A must never access Tenant B's carrier precedents, documents, or knowledge chunks.
4. **Reliability & Async Decoupling (Rule 10):** Slow external model calls must never be executed inside database transactions or block synchronous HTTP request threads.
5. **Scope Optimization:** In accordance with user guidance, heavy image-based Optical Character Recognition (OCR) dependencies (e.g. Tesseract, cloud OCR) are omitted in favor of direct text-based document parsing and a dedicated RAG subsystem.

---

## Decision

### 1. Dedicated AI Module & Persistence Model
- Created `ResolveOps.Modules.Ai` following Vertical Slice Architecture and registered in `ResolveOps.slnx`.
- Implemented core domain entities in `ResolveOps.Domain.Ai`:
  - **`AiTask`:** Stores asynchronous task execution, input hash (SHA-256), model provider, model name, prompt version, structured JSON output, confidence score, and review status.
  - **`AiFeedback`:** Captures field-level human corrections (`field_path`, `original_value`, `corrected_value`) for continuous evaluation.
  - **`AiKnowledgeEmbedding`:** Stores tenant-partitioned knowledge chunks, carrier associations, metadata, and normalized vector embeddings.
- Configured EF Core global query filters and composite database indexes:
  - `ix_ai_tasks_tenant_task_status` on `(tenant_id, task_type, status)`
  - `ix_ai_tasks_tenant_source` on `(tenant_id, source_entity_type, source_entity_id)`
  - `ix_ai_knowledge_tenant_type_carrier` on `(tenant_id, knowledge_type, carrier_id)`

### 2. Tenant-Partitioned RAG & Vector Embeddings Subsystem
- Designed `IEmbeddingService` with pluggable providers:
  - `DeterministicMockEmbeddingProvider`: Produces deterministic 64-dimensional L2-normalized float vectors via token frequency hashing, allowing fast, offline, and repeatable vector similarity testing without third-party API dependencies.
  - Provider adapter for external embedding services (OpenAI `text-embedding-3-small` / Ollama `nomic-embed-text`).
- Implemented `AiKnowledgeRetrievalService`:
  - Ingests carrier claim policies, dispute SOPs, and successful claim precedents.
  - Computes Cosine Similarity strictly partitioned by `tenant_id` at the database query level.
  - Exposes `GET /api/ai/semantic-search` for natural language precedent lookup.

### 3. Untrusted Data Boundary & Prompt Injection Shielding (§19.8)
- Implemented `PromptInjectionShield`:
  - Encapsulates all third-party emails, notes, and unstructured text inside `<untrusted_data>...</untrusted_data>` blocks.
  - Appends mandatory system security directive instructing the LLM to treat content inside the tags strictly as data, never as instructions.
  - Scans for adversarial keywords (jailbreaks, DAN mode, system prompt overrides, financial auto-approval attempts) and neutralizes them with quarantine responses.
- Implemented `PiiSanitizer` to redact credit cards, JWT tokens, API keys, and credentials prior to prompt assembly.

### 4. Pluggable LLM Completion & Structured Outputs
- Implemented `IAiCompletionService` with `DeterministicMockAiCompletionService` and external API adapter.
- Enforces strict JSON Schema validation for all 5 use cases:
  1. **Email & Note Classification:** Returns `EmailClassificationOutput` (candidate exception types, severity, tracking number, package count, POD flags).
  2. **Timeline Summarization with Source Citations:** Generates chronological case summaries where every statement references timeline entry IDs or document IDs.
  3. **RAG-Driven Evidence Recommendation:** Queries carrier policy chunks to identify missing evidence requirements (advisory only).
  4. **RAG-Driven Draft Communication:** Drafts formal appeal letters citing carrier SLA terms and precedent resolutions (requires human approval).
  5. **Semantic Search:** Natural language search over historical tenant cases.

### 5. Confidence Policy Engine & Human Review Queue (§21.5)
- Implemented `ConfidencePolicyEngine`:
  - Confidence $\ge 0.90$ (low-risk tasks): Marked as prefill suggestion for one-click human confirmation (`AutoApplied`).
  - Confidence $0.70$–$0.89$: Displayed with warning banner (`PendingReview`).
  - Confidence $< 0.70$: Enqueued to mandatory human review queue (`PendingReview`).
  - **Financial Decisions:** Strictly requires human decision regardless of confidence score (Rule 4).
- Implemented Human Review Minimal APIs:
  - `GET /api/ai/review-queue`
  - `GET /api/ai/tasks/{id}`
  - `POST /api/ai/tasks/{id}/review`
  - `POST /api/ai/feedback`

### 6. Asynchronous Processing & Outbox Integration (Rule 10)
- Defined `AiTaskRequestedV1` integration event written to the transactional outbox (`IOutboxWriter`).
- Implemented `AiProcessingConsumerService` in `ResolveOps.Worker` consuming RabbitMQ queue `resolveops.ai-processing` with transient retry and DLQ routing (`resolveops.dlx`).
- Preserves Rule 10: Model invocations execute outside of database transactions.

### 7. Kill Switch Governance
- Built `AiFeatureFlagService` and `AiOptions` supporting instant global disablement and per-tenant feature toggles without impacting deterministic core operations.

---

## Consequences

### Positive
- Strict compliance with Rule 4 & Invariant 12: Zero risk of unauthorized automated financial state mutations.
- Multi-tenant data leakage prevented through database-level `tenant_id` partitioning on all vector queries.
- Zero external runtime API dependencies required for local testing, CI evaluation, and benchmarking via deterministic mock providers.
- Anti-hallucination enforced on timeline summaries via mandatory source citations.

### Neutral / Trade-offs
- Heavy OCR on raw scanned images is waived; ingestion relies on text-based document parsing and direct input text.
- Offline vector embeddings use 64-dimensional mock projections; production deployment with real external embeddings can be toggled via configuration without code changes.
