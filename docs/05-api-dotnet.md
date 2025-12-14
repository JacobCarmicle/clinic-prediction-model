1. Purpose

The ASP.NET Core API serves as the orchestration layer for the system. It:

- Validates and ingests schedules
- Calls the ML inference service
- Invokes the C-based simulation engine
- Aggregates results into actionable recommendations
- Enforces rate limits and demo constraints

This layer contains no ML logic and no heavy computation — it coordinates.

2. Technology Stack

- ASP.NET Core Web API (.NET 8)
- Minimal APIs or Controllers
- JSON-first (CSV supported via upload)
- P/Invoke for C simulation engine
- Optional in-memory caching (demo mode)

3. High-Level API Flow
Client
  ↓
POST /schedule/analyze
  ↓
Validate + Normalize Input
  ↓
ML Inference (No-show probabilities)
  ↓
C Simulation Engine (Monte Carlo)
  ↓
Aggregate Metrics
  ↓
JSON Response (Results + Recommendations)

4. API Endpoints
4.1 Health & Metadata
GET /health

Used for monitoring and demo uptime.

```json
{ "status": "ok" }
```

4.2 Analyze a Schedule
POST /schedule/analyze

Runs inference + simulation for the uploaded schedule.

**Request**

```json
{
  "clinic_profile": {
    "clinic_type": "PrimaryCare",
    "providers_per_block": 3,
    "avg_service_minutes": 15
  },
  "appointments": [ ... ]
}
```

**Response**

```json
{
  "summary": {
    "expected_kept_appointments": 42.3,
    "overload_probability": 0.04,
    "expected_wait_minutes": 7.1,
    "revenue_recovered": 510.0
  },
  "slot_predictions": [
    {
      "appointment_id": 12,
      "no_show_probability": 0.27,
      "confidence_interval": [0.21, 0.33]
    }
  ]
}
```

4.3 Simulate a Strategy
POST /schedule/simulate

Runs what-if simulations with overbooking rules.

**Request**

```json
{
  "strategy": {
    "overbook_percent_by_segment": {
      "Tuesday-Morning": 0.15,
      "Friday-Afternoon": 0.10
    }
  },
  "num_trials": 5000
}
```

**Response**

```json
{
  "strategy_name": "Targeted AM Overbooking",
  "metrics": {
    "expected_kept_appointments": 45.8,
    "overload_probability": 0.06,
    "expected_wait_minutes": 9.4,
    "revenue_recovered": 740.0
  }
}
```

4.4 Optimize Scheduling Policy
POST /schedule/optimize

Explores multiple strategies and recommends safe overbooking zones.

**Request**

```json
{
  "constraints": {
    "max_overload_probability": 0.05,
    "max_expected_wait_minutes": 10
  }
}
```

**Response**

```json
{
  "recommended_strategy": {
    "Tuesday-Morning": 0.12,
    "Thursday-Morning": 0.10
  },
  "expected_metrics": {
    "revenue_recovered": 680.0,
    "overload_probability": 0.048
  }
}
```

5. DTOs & Validation

**Required Validation**

- Enum correctness
- Probability bounds
- Non-negative revenue
- Required fields present

**Validation Failures**

Return 400 Bad Request with structured errors:

```json
{
  "error": "ValidationFailed",
  "details": [
    "historical_no_show_rate must be between 0 and 1"
  ]
}
```

6. ML Integration

**Inference Options**

- In-process ONNX runtime
- Python microservice (FastAPI) over HTTP (optional)

**API responsibility**

- Convert DTO → feature vector
- Batch inference
- Attach predictions back to appointments

7. C Engine Integration

**P/Invoke Binding**

```csharp
[DllImport("simulation.dll")]
public static extern int run_simulation(
    Appt[] appts,
    uint nAppts,
    ref SimConfig config,
    out SimResult result
);
```

**Responsibilities:**

- Memory-safe marshaling
- Batch strategy execution
- Error-code handling

8. Rate Limiting & Demo Mode

**Public Demo Constraints**

- Max appointments per request (e.g., 200)
- Max strategies per optimize call
- Fixed num_trials ceiling
- Anonymous usage

**Private Mode (Later)**

- Auth (JWT)
- Higher limits
- Persistent configs

9. Error Handling Strategy

| Error Type | HTTP Code |
|------------|-----------|
| Validation error | 400 |
| Simulation error | 500 |
| ML unavailable | 503 |
| Rate limit exceeded | 429 |

10. Logging & Observability

**Logged (safe):**

- request size
- runtime metrics
- error codes
- drift warnings

**Not logged:**

- raw appointment data
- any identifiers

11. Deliverables

- ASP.NET Core Web API
- DTOs + validation layer
- ML inference client
- C engine bindings
- Rate limiting middleware
- Demo-safe configuration