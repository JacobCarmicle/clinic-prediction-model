1. Scope & Purpose

This document formally defines the functional and non-functional requirements for the Clinic No-Show & Schedule Optimization System.

These requirements serve as:

a design contract

a validation baseline

a reference for testing and future expansion

2. Functional Requirements (FR)
FR-1 — Schedule Ingestion

The system shall accept clinic schedules via JSON and CSV upload.

Details

Must support batch appointment slots

Must validate against the approved schema

Must reject unknown or unsafe fields

Priority: Must-have (MVP)

FR-2 — Input Validation & Normalization

The system shall validate and normalize all incoming data before processing.

Includes

enum validation

probability bounds checking

default value injection

segment assignment (day × time × type)

Priority: Must-have

FR-3 — No-Show Probability Prediction

The system shall predict a no-show probability for each appointment slot using a trained ML model.

Output

probability value (0–1)

optional confidence interval

optional top drivers

Priority: Must-have

FR-4 — Monte Carlo Schedule Simulation

The system shall simulate schedule outcomes under uncertainty using probabilistic attendance.

Simulation must compute

expected kept appointments

overload probability

expected wait time

revenue recovered

Priority: Must-have

FR-5 — Overbooking Strategy Simulation

The system shall allow users to define and simulate overbooking strategies segmented by time and appointment type.

Priority: Must-have

FR-6 — Scheduling Policy Optimization

The system shall evaluate multiple candidate strategies and recommend a policy that satisfies operational constraints.

Constraints include

maximum overload probability

maximum expected wait time

Priority: Must-have

FR-7 — Results Explanation

The system shall explain recommendations using interpretable metrics.

Includes

why a segment is safe/unsafe

impact deltas vs baseline

Priority: Should-have

FR-8 — Public Demo Mode

The system shall support anonymous public usage with enforced limits.

Priority: Must-have

FR-9 — Error Reporting

The system shall return structured error responses for validation and runtime failures.

Priority: Must-have

FR-10 — Health & Observability

The system shall expose health and runtime status endpoints.

Priority: Should-have

3. Non-Functional Requirements (NFR)
3.1 Performance

| Requirement | Target |
|-------------|--------|
| API response (analyze) | ≤ 2s (200 slots, demo mode) |
| Simulation runtime | ≤ 100ms per strategy (5k trials) |
| Inference latency | ≤ 1ms per slot (batch) |
3.2 Scalability

Must support ≥ 200 appointments per request (demo)

Architecture must support horizontal scaling

Strategy search bounded in demo mode

3.3 Reliability

Graceful degradation if ML inference fails

Simulation failures return partial results + warnings

Deterministic simulation when seed is fixed

3.4 Security

HTTPS-only communication

Strict input schema enforcement

No execution of user-supplied code

Rate limiting enabled for demo mode

3.5 Privacy & Compliance

No PHI ingestion, storage, or logging

Logs limited to safe operational metadata

Memory-only processing in demo mode

3.6 Usability

API responses must be human-readable

Errors must clearly state cause and resolution

Outputs structured for easy visualization

3.7 Maintainability

Modular separation of ML, simulation, API

Stable data contracts

Clear error boundaries between layers

3.8 Testability

Deterministic simulation supported

ML inference mockable

API endpoints testable via contract tests

3.9 Portability

Must run on Linux containers

No GPU dependency

No proprietary runtime dependencies

4. Traceability Matrix (FR → System Component)

| FR | API | ML | C Sim |
|----|-----|----|----|
| FR-1 | ✔ | | |
| FR-3 | ✔ | ✔ | |
| FR-4 | ✔ | | ✔ |
| FR-6 | ✔ | ✔ | ✔ |
| FR-8 | ✔ | | |

This is very professional and interview-ready.

5. Acceptance Criteria Summary

The system is considered compliant when:

all MUST requirements are implemented

NFR performance targets are met under demo constraints

no PHI paths exist in code or logs