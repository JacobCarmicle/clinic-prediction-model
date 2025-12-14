1. Purpose

The purpose of this document is to:

- identify known risks early
- justify key architectural decisions
- document assumptions and limitations
- guide future iteration and mitigation

This analysis applies to both public demo and future private deployments.

2. Key Assumptions

The system is designed under the following assumptions:

- input data is operational and anonymized
- no patient-level identifiers are present
- clinic capacity can be reasonably approximated
- no-show behavior is statistically learnable from aggregate features
- public demo traffic will be moderate

If these assumptions break, model accuracy or system behavior may degrade.

3. Technical Risks
R1 — Synthetic or Limited Training Data

**Risk**
The public demo model may be trained on synthetic or limited datasets, reducing real-world accuracy.

**Impact**

- lower predictive performance
- miscalibrated probabilities

**Mitigation**

- emphasize simulation + relative comparisons over absolute predictions
- expose confidence intervals
- position demo as "directional insights," not guarantees
- retrain with real clinic aggregates in private mode

R2 — Simplified Wait-Time Model

**Risk**
The MVP uses a simplified wait-time approximation rather than a full discrete-event simulation.

**Impact**

- wait estimates may be optimistic or coarse
- may not capture arrival clustering

**Mitigation**

- document model assumptions clearly
- include p95 wait metrics to reflect tail risk
- future upgrade path to discrete-event simulation

**Tradeoff**

**Accuracy vs Performance**

- chosen simplicity enables fast simulation on free-tier CPUs

R3 — Monte Carlo Cost at Scale

**Risk**
Large schedules or many strategies increase computational cost.

**Impact**

- higher latency
- potential demo instability

**Mitigation**

- hard limits in demo mode
- bounded strategy grids
- batch simulation support in C
- deterministic seeds for reproducibility

R4 — Model Drift Over Time

**Risk**
No-show behavior changes seasonally or operationally.

**Impact**

- reduced prediction accuracy
- unsafe recommendations if unchecked

**Mitigation**

- drift detection metrics (PSI, prediction shift)
- confidence penalty during drift
- warnings surfaced to users
- retraining pipeline planned

4. Architectural Tradeoffs
T1 — C Simulation Engine vs Managed Language

**Decision**
Use native C for Monte Carlo simulation.

**Benefits**

- high performance
- deterministic behavior
- low runtime overhead

**Costs**

- increased complexity
- P/Invoke maintenance
- memory safety responsibility

**Rationale**
Simulation performance is a core differentiator; complexity is justified.

T2 — In-Process ML Inference vs Separate Service

**Decision**
Default to in-process inference (ONNX runtime).

**Benefits**

- lower latency
- simpler deployment
- free-tier friendly

**Costs**

- tighter coupling
- less flexible scaling

**Rationale**
Public demo prioritizes simplicity and cost; service split remains an option.

T3 — Optimization via Grid Search

**Decision**
Use bounded grid search for strategy optimization.

**Benefits**

- explainable
- deterministic
- easy to constrain

**Costs**

- not globally optimal
- slower than gradient-based methods at large scale

**Rationale**
Transparency and safety are more important than theoretical optimality in healthcare ops.

5. Privacy & Compliance Risks
R5 — Accidental PHI Submission by Users

**Risk**
Users may upload schedules containing identifiers.

**Impact**

- compliance concerns
- trust erosion

**Mitigation**

- strict schema enforcement
- rejection of unknown fields
- user-facing warnings
- no raw payload logging

R6 — Data Leakage Through Logs

**Risk**
Sensitive information could appear in logs.

**Mitigation**

- structured logging only
- whitelist-based fields
- no request body logging
- redaction by default

6. Product & Adoption Risks
R7 — Misinterpretation of Predictions

**Risk**
Users may treat probabilities as guarantees.

**Impact**

- poor operational decisions
- loss of trust

**Mitigation**

- emphasize simulation distributions
- show uncertainty bands
- frame outputs as decision support, not automation

R8 — Overconfidence in Recommendations

**Risk**
Clinics may blindly apply recommendations.

**Mitigation**

- configurable constraints
- explainability requirements
- conservative defaults
- warnings when margins are thin

7. Operational Risks
R9 — Free-Tier Resource Limits

**Risk**
Public demo exceeds free-tier limits.

**Mitigation**

- strict rate limiting
- request size caps
- lightweight telemetry
- graceful degradation

8. Known Limitations (Explicit)

The system does not:

- guarantee optimal schedules
- replace human judgment
- model individual patient behavior
- integrate directly with EHR systems
- account for all real-world disruptions (traffic, emergencies)

These are intentional scope limits.

9. Risk Summary Table

| Risk | Severity | Mitigation |
|------|----------|------------|
| Synthetic data | Medium | Confidence intervals, positioning |
| Simplified waits | Medium | Documentation, p95 metrics |
| Compute cost | Medium | Limits, batching |
| Drift | High | Detection + warnings |
| PHI submission | High | Schema enforcement |
| Misuse | Medium | Explainability + constraints |
10. Why This Document Matters

Including this analysis shows:

- awareness of real-world complexity
- engineering judgment
- leadership-level thinking
- readiness for production constraints