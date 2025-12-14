1. Purpose

This document defines how the system remains HIPAA-safe by design by ensuring that:

no Protected Health Information (PHI) is collected, stored, or processed

inputs are strictly operational and anonymized

logs and telemetry cannot reconstruct patient identity

the public demo can be safely deployed with zero patient risk

2. HIPAA-Safe Boundary
2.1 What We Allow (Operational Only)

Allowed inputs are limited to:

appointment metadata (type, time block, day of week)

operational scheduling factors (lead time, capacity)

aggregate rates (historical no-show rate by category)

non-identifying clinic profile info (specialty, service time)

2.2 What We Prohibit (PHI / Identifiers)

The system must reject or never accept:

names, initials

DOB or age if too specific (optional: allow “age bucket” only in private mode)

phone/email/address

MRN / patient IDs

free-text notes

diagnosis, procedure codes tied to specific patients

insurance member IDs

3. Data Minimization Rules

The API accepts only fields defined in docs/02-data-model.md.

Hard rule:

If a field is not in the schema, it is ignored or rejected (configurable), and never logged.

4. Storage Policy
Public Demo Mode

No database required

Requests processed in-memory

Optional ephemeral caching (short TTL) for performance

No per-user persistence

Private Deployment (Future)

Store only:

aggregated model training data (not raw schedules)

clinic-level configs (capacity, service times)

strategy results (optional)

If storing any schedule-like records, enforce:

schema-only fields

TTL expiration

encryption at rest

5. Logging & Telemetry (Critical)
Safe to Log

request id

timestamp

number of appointments

endpoint used

compute time (ms)

error codes

drift warnings / model version

Never Log

appointment arrays in raw form

user-uploaded CSV content

any potential identifier fields

request bodies on error

Redaction Policy

structured logging only

automatic redaction for string fields (if strings are allowed at all)

truncate any unexpected payload fields before logging

6. Security Controls
6.1 Transport Security

HTTPS only

HSTS enabled (when applicable)

6.2 Input Validation

strict schema validation

bounds checking (probabilities ∈ [0,1], lead_time ≥ 0, etc.)

max payload size

reject unknown fields (recommended in production)

6.3 Rate Limiting

Public demo:

per-IP rate limiting

request size limits (appointments, trials, strategies)

burst control

6.4 Dependency Security

pinned versions for .NET dependencies

SBOM generation (optional)

container scanning (if containerized)

7. Threat Model (Practical)
Threat: User uploads PHI accidentally

Mitigation:

schema rejects extra columns / unknown JSON fields

CSV parser enforces allowed header list

error messaging instructs user to remove identifiers

Threat: PHI leaks through logs

Mitigation:

never log request bodies

structured logging only

safe fields whitelist

Threat: Model inversion / re-identification

Mitigation:

inputs are aggregated and categorical

model never trained on patient identifiers

no patient-level features stored

8. Compliance Positioning

This project is designed to be:

HIPAA-safe for public demo because it does not handle PHI

suitable as a “non-PHI operational analytics tool”

However:

any real clinic deployment should still undergo a formal security review and contractual agreement decisions (BAA, etc.) depending on how they integrate it.

9. User-Facing Privacy Notice (Demo Mode)

Suggested short notice to display on the demo UI:

“Do not upload patient identifiers or clinical notes. This tool is designed for operational scheduling data only and does not store uploaded schedules.”

10. Deliverables

strict schema validation rules

logging whitelist + redaction policy

public demo privacy notice

security checklist for deployment