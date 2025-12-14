1. Design Principles

The data model is designed to be:

HIPAA-safe by construction

Appointment-centric, not patient-centric

Aggregated and categorical

Consistent across ML, simulation, and API layers

Public-demo friendly

No field may directly or indirectly identify a patient.

2. Core Entity: Appointment Slot

Each record represents one scheduled appointment slot (real or simulated).

```json
{
  "appointment_type": "FollowUp",
  "day_of_week": "Tuesday",
  "time_block": "Morning",
  "lead_time_days": 14,
  "is_new_patient": false,
  "clinic_specialty": "PrimaryCare",
  "historical_no_show_rate": 0.18,
  "slot_capacity": 1,
  "unit_revenue": 120.00
}
```

3. Feature Definitions

3.1 Categorical Features

| Feature | Type | Description |
|---------|------|-------------|
| appointment_type | enum | NewPatient, FollowUp, Procedure, Lab, Other |
| day_of_week | enum | Monday–Sunday |
| time_block | enum | Morning, Midday, Afternoon, Evening |
| clinic_specialty | enum | PrimaryCare, Dental, PT, Behavioral, Other |

3.2 Boolean Features

| Feature | Type | Description |
|---------|------|-------------|
| is_new_patient | bool | True if first visit (derived, not ID-based) |

3.3 Numeric Features

| Feature | Type | Range | Description |
|---------|------|-------|-------------|
| lead_time_days | int | ≥ 0 | Days between booking and appointment |
| historical_no_show_rate | float | 0–1 | Aggregated rate for this slot category |
| slot_capacity | int | ≥ 1 | Appointments per slot (used in simulation) |
| unit_revenue | float | ≥ 0 | Expected revenue per kept appointment |
4. Optional / Advanced Features

These are not required for MVP, but supported by schema:

| Feature | Purpose |
|---------|---------|
| season | Capture seasonal drift |
| weather_index | Proxy for external disruption |
| lead_time_bucket | Discretized lead-time |
| provider_type | MD, NP, PA (no identity) |
| historical_stddev | Variability in arrivals |
5. Input Contracts

5.1 JSON Input (API)

```json
{
  "clinic_profile": {
    "clinic_type": "PrimaryCare",
    "providers_per_block": 3,
    "avg_service_minutes": 15
  },
  "appointments": [ /* array of appointment slot objects */ ]
}
```

5.2 CSV Input (Upload)

Required columns:

appointment_type,day_of_week,time_block,lead_time_days,is_new_patient,historical_no_show_rate,unit_revenue


Optional columns are ignored if absent.

6. Output Schema

6.1 ML Inference Output

```json
{
  "no_show_probability": 0.27,
  "confidence_interval": [0.21, 0.33],
  "top_drivers": ["lead_time_days", "appointment_type"]
}
```

6.2 Simulation Output

```json
{
  "expected_kept_appointments": 42.3,
  "overload_probability": 0.04,
  "expected_wait_minutes": 7.1,
  "revenue_recovered": 510.00
}
```

7. Validation Rules

**Hard Validation**

- Enum values must match allowed set
- Probabilities ∈ [0,1]
- Revenue ≥ 0
- No nulls in required fields

**Soft Validation (Warnings)**

- Sparse historical data
- Extreme lead times
- Rare appointment types

8. Privacy & Safety Guarantees

The schema explicitly excludes:

- Names
- Dates of birth
- Contact info
- Medical data
- Free-text notes

All fields are:

- Aggregated
- Operational
- Non-identifying

9. Data Flow Consistency
CSV / JSON
   ↓
Validation
   ↓
ML Feature Vector
   ↓
No-Show Probability
   ↓
C Simulation Engine
   ↓
Metrics + Recommendations

10. Deliverables

- Stable feature schema
- Input/output contracts
- Validation rules
- Demo-compatible dataset format