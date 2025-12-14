1. Purpose

This document defines the testing strategy for the Clinic No-Show & Schedule Optimization System across all layers:

API

ML inference

C simulation engine

Cross-layer integration

The goal is to ensure:

correctness

stability

performance

safety (HIPAA-safe behavior)

2. Testing Philosophy

The system follows a layered testing approach:

Unit Tests
   ↓
Component Tests
   ↓
Integration Tests
   ↓
System / Acceptance Tests


Key principles:

deterministic where possible

probabilistic behavior tested statistically

validation over raw accuracy

failures should degrade gracefully

3. Test Layers & Scope
3.1 Unit Testing
API Layer

What

DTO validation

enum parsing

bounds checking

segment generation logic

Why

Prevent unsafe or malformed data from entering the system

Examples

reject probability < 0 or > 1

reject unknown enum values

default service time injected when missing

ML Layer

What

feature encoding correctness

inference output bounds

calibration logic

Why

Model outputs feed directly into simulation

Examples

probabilities always ∈ [0,1]

confidence intervals are ordered and valid

fallback logic when model unavailable

C Simulation Engine

What

PRNG correctness

capacity calculation

overload detection

wait-time computation

Why

Simulation correctness is the system’s differentiator

Examples

p(no-show)=0 → all attend

p(no-show)=1 → none attend

increasing overbooking increases overload probability (monotonic expectation)

4. Component Testing
4.1 ML + Simulation Boundary

Goal
Verify that ML outputs are correctly consumed by the simulation engine.

Tests

fixed probabilities → expected kept appointments

deterministic results with fixed seed

sensitivity tests (small Δp → small Δmetric)

4.2 Strategy Evaluation

Goal
Ensure strategies are applied correctly by segment.

Tests

overbooking applied only to intended segments

percent-based vs count-based conversions

baseline strategy equals “no overbooking”

5. Integration Testing
5.1 API End-to-End Tests

Endpoints

/schedule/analyze

/schedule/simulate

/schedule/optimize

Tests

valid schedule → 200 OK + results

invalid schema → 400 + structured errors

extreme inputs → graceful handling

5.2 Optimization Flow

Goal
Ensure optimization returns a valid strategy under constraints.

Tests

constraint respected (overload ≤ max)

returns null / warning if no valid strategy exists

best strategy beats baseline in objective metric

6. System / Acceptance Testing
6.1 Functional Requirement Traceability

| FR | Test Type | Description |
|----|-----------|-------------|
| FR-1 | API | Schedule upload accepted/rejected correctly |
| FR-3 | ML | No-show probabilities generated |
| FR-4 | C Sim | Monte Carlo metrics computed |
| FR-6 | Integration | Best strategy selected |
| FR-8 | System | Demo limits enforced |
6.2 Demo Mode Safety Tests

**Ensure**

- no PHI fields accepted
- payload size limits enforced
- logs contain no raw schedule data

7. Performance Testing

**Targets**

- analyze endpoint < 2s (demo limits)
- simulation < 100ms per strategy
- inference < 1ms per slot (batch)

**Methods**

- benchmark harness for C engine
- load testing for API (lightweight)
- cold-start timing (containerized)

8. Statistical Validation (Simulation & ML)

Because outputs are probabilistic:

**Monte Carlo Stability Tests**

- run with increasing trials (1k → 5k → 10k)
- verify metric convergence

**ML Calibration Tests**

- reliability curves
- Brier score thresholds
- over/under-confidence detection

9. Regression Testing

**Triggered when**

- model retrained
- simulation logic changes
- schema updated

**Strategy**

- golden test schedules
- snapshot metric comparison with tolerance bands

10. Failure & Degradation Tests

**Simulated Failures**

- ML unavailable
- simulation returns error
- cache disabled

**Expected Behavior**

- partial results returned when possible
- warnings surfaced
- system does not crash or leak data

11. Test Automation (Planned)

- unit tests run on every commit
- integration tests run pre-deploy
- benchmark thresholds checked manually (initially)

12. Exit Criteria

The system is considered test-ready when:

- all MUST FRs have at least one passing test
- simulation sanity tests pass
- demo safety tests pass
- performance targets met under demo limits