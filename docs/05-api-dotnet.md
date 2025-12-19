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

3. High-Level API Flow & Phases

The API operates in three phases, progressively adding sophistication:

**Phase A: Deterministic Baseline (Current MVP)**
- Uses historical no-show rates directly (no ML inference)
- Computes expected kept appointments: `Σ(1 - historical_no_show_rate)`
- Calculates capacity and overflow metrics
- Returns segment-level breakdowns
- Fast, explainable, suitable for baseline analysis

**Phase B: ML-Enhanced Predictions (Future)**
- Replaces historical rates with ML model predictions
- Per-slot no-show probabilities from trained model
- Confidence intervals and feature drivers
- Still deterministic expected values (no Monte Carlo)

**Phase C: Monte Carlo Simulation (Future)**
- Uses ML predictions as input to Monte Carlo engine
- Samples attendance outcomes across many trials
- Returns distributions, not just point estimates
- Enables risk-based decision making

**Current Implementation:**
- `/schedule/analyze` → Phase A (deterministic baseline)
- `/schedule/simulate` → Phase A with strategy overbooking (deterministic)

**Future Evolution:**
- Phase B: ML inference service integration
- Phase C: C-based Monte Carlo engine via P/Invoke

4. API Endpoints
4.1 Health & Metadata
GET /health

Used for monitoring and demo uptime.

```json
{ "status": "ok" }
```

4.2 Analyze a Schedule
POST /schedule/analyze

**Phase:** A (Deterministic Baseline)

Computes baseline metrics using historical no-show rates. Returns overall summary, per-slot predictions (stub), and segment-level breakdowns.

**Request**

```json
{
  "clinicProfile": {
    "clinicType": "PrimaryCare",
    "providersPerBlock": 3,
    "avgServiceMinutes": 15,
    "timeBlockMinutes": 240
  },
  "appointments": [
    {
      "appointmentId": 1,
      "appointmentType": "FollowUp",
      "dayOfWeek": "Tuesday",
      "timeBlock": "Morning",
      "leadTimeDays": 14,
      "isNewPatient": false,
      "historicalNoShowRate": 0.18,
      "unitRevenue": 120.0
    }
  ]
}
```

**Response**

```json
{
  "summary": {
    "expectedKeptAppointments": 42.3,
    "baselineRevenue": 5076.0,
    "blockCapacityAppointments": 48.0,
    "expectedOverflowAppointments": 0.0,
    "isOverCapacity": false
  },
  "slotPredictions": [
    {
      "appointmentId": 1,
      "noShowProbability": 0.18
    }
  ],
  "segments": [
    {
      "segmentKey": "Tuesday-Morning",
      "expectedKeptAppointments": 12.5,
      "baselineRevenue": 1500.0,
      "capacityAppointments": 48.0,
      "expectedOverflowAppointments": 0.0,
      "isOverCapacity": false
    }
  ]
}
```

**Response Fields:**
- `summary`: Overall clinic-wide metrics
- `slotPredictions`: Per-appointment no-show probabilities (currently uses historical rates as stub)
- `segments`: Metrics grouped by `DayOfWeek-TimeBlock` segments

4.3 Simulate a Strategy
POST /schedule/simulate

**Phase:** A (Deterministic with Strategy Overbooking)

Simulates the impact of an overbooking strategy by adding appointments to specified segments. Returns baseline vs simulated comparison with deltas and per-segment breakdowns.

**Request**

```json
{
  "clinicProfile": {
    "clinicType": "PrimaryCare",
    "providersPerBlock": 3,
    "avgServiceMinutes": 15,
    "timeBlockMinutes": 240
  },
  "appointments": [
    {
      "appointmentId": 1,
      "appointmentType": "FollowUp",
      "dayOfWeek": "Tuesday",
      "timeBlock": "Morning",
      "leadTimeDays": 14,
      "isNewPatient": false,
      "historicalNoShowRate": 0.18,
      "unitRevenue": 120.0
    }
  ],
  "strategy": {
    "name": "Targeted AM Overbooking",
    "overbookAdditionsBySegment": {
      "Tuesday-Morning": 5,
      "Friday-Afternoon": 3
    }
  }
}
```

**Response**

```json
{
  "strategyName": "Targeted AM Overbooking",
  "baselineSummary": {
    "expectedKeptAppointments": 42.3,
    "baselineRevenue": 5076.0,
    "blockCapacityAppointments": 48.0,
    "expectedOverflowAppointments": 0.0,
    "isOverCapacity": false
  },
  "simulatedSummary": {
    "expectedKeptAppointments": 46.4,
    "baselineRevenue": 5568.0,
    "blockCapacityAppointments": 48.0,
    "expectedOverflowAppointments": 0.0,
    "isOverCapacity": false
  },
  "delta": {
    "deltaExpectedKeptAppointments": 4.1,
    "deltaRevenue": 492.0,
    "deltaExpectedOverflowAppointments": 0.0
  },
  "segments": [
    {
      "segmentKey": "Tuesday-Morning",
      "baseline": {
        "segmentKey": "Tuesday-Morning",
        "expectedKeptAppointments": 12.5,
        "baselineRevenue": 1500.0,
        "capacityAppointments": 48.0,
        "expectedOverflowAppointments": 0.0,
        "isOverCapacity": false
      },
      "simulated": {
        "segmentKey": "Tuesday-Morning",
        "expectedKeptAppointments": 16.6,
        "baselineRevenue": 1992.0,
        "capacityAppointments": 48.0,
        "expectedOverflowAppointments": 0.0,
        "isOverCapacity": false
      }
    }
  ]
}
```

**Response Fields:**
- `strategyName`: Name of the strategy being evaluated
- `baselineSummary`: Overall metrics without overbooking
- `simulatedSummary`: Overall metrics with strategy applied
- `delta`: Difference between simulated and baseline (positive = improvement)
- `segments`: Per-segment comparison showing baseline vs simulated for each segment

4.4 Optimize Scheduling Policy
POST /schedule/optimize

**Phase:** A (Deterministic Grid Search)

Explores multiple overbooking strategies using grid search and recommends the best strategy that meets operational constraints. Tests single-segment additions across all segments, evaluates each using simulation, and returns the top candidates.

**Request**

```json
{
  "clinicProfile": {
    "clinicType": "PrimaryCare",
    "providersPerBlock": 3,
    "avgServiceMinutes": 15,
    "timeBlockMinutes": 240
  },
  "appointments": [
    {
      "appointmentId": 1,
      "appointmentType": "FollowUp",
      "dayOfWeek": "Tuesday",
      "timeBlock": "Morning",
      "leadTimeDays": 14,
      "isNewPatient": false,
      "historicalNoShowRate": 0.18,
      "unitRevenue": 120.0
    }
  ],
  "constraints": {
    "maxExpectedOverflowAppointments": 5.0,
    "minDeltaRevenue": 100.0
  },
  "search": {
    "maxAdditionPerSegment": 10,
    "maxTotalAdditions": 20,
    "allowedSegments": ["Tuesday-Morning", "Friday-Afternoon"]
  }
}
```

**Response**

```json
{
  "baselineSummary": {
    "expectedKeptAppointments": 42.3,
    "baselineRevenue": 5076.0,
    "blockCapacityAppointments": 48.0,
    "expectedOverflowAppointments": 0.0,
    "isOverCapacity": false
  },
  "bestSummary": {
    "expectedKeptAppointments": 46.4,
    "baselineRevenue": 5568.0,
    "blockCapacityAppointments": 48.0,
    "expectedOverflowAppointments": 2.1,
    "isOverCapacity": false
  },
  "delta": {
    "deltaExpectedKeptAppointments": 4.1,
    "deltaRevenue": 492.0,
    "deltaExpectedOverflowAppointments": 2.1
  },
  "recommendedStrategy": {
    "name": "Add 5 to Tuesday-Morning",
    "overbookAdditionsBySegment": {
      "Tuesday-Morning": 5
    }
  },
  "topCandidates": [
    {
      "strategyName": "Add 5 to Tuesday-Morning",
      "overbookAdditionsBySegment": {
        "Tuesday-Morning": 5
      },
      "summary": {
        "expectedKeptAppointments": 46.4,
        "baselineRevenue": 5568.0,
        "blockCapacityAppointments": 48.0,
        "expectedOverflowAppointments": 2.1,
        "isOverCapacity": false
      },
      "delta": {
        "deltaExpectedKeptAppointments": 4.1,
        "deltaRevenue": 492.0,
        "deltaExpectedOverflowAppointments": 2.1
      }
    }
  ]
}
```

**Request Fields:**
- `clinicProfile`: Clinic configuration (same as analyze/simulate)
- `appointments`: Base schedule to optimize
- `constraints.maxExpectedOverflowAppointments`: Maximum allowed overflow (double, >= 0)
- `constraints.minDeltaRevenue`: Minimum revenue improvement required (double?, nullable, optional)
- `search.maxAdditionPerSegment`: Maximum appointments to add per segment (int, 0-25)
- `search.maxTotalAdditions`: Maximum total appointments across all segments (int, 0-50)
- `search.allowedSegments`: Optional list of segment keys to consider (null = all segments)

**Response Fields:**
- `baselineSummary`: Overall metrics without overbooking
- `bestSummary`: Overall metrics with recommended strategy applied
- `delta`: Difference between best and baseline
- `recommendedStrategy`: Best strategy meeting constraints (maximizes revenue, minimizes overflow)
- `topCandidates`: Top 10 candidate strategies ranked by revenue delta

**Optimization Algorithm:**
1. Computes baseline metrics
2. Generates candidates: tries additions from 0 to `maxAdditionPerSegment` for each segment
3. Filters by constraints: removes candidates exceeding `maxExpectedOverflowAppointments` or below `minDeltaRevenue`
4. Selects best: maximizes `deltaRevenue`, tie-breaks by minimizing `expectedOverflowAppointments`
5. Returns top 10 candidates for transparency

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
    {
      "field": "appointments[0].historicalNoShowRate",
      "message": "historicalNoShowRate must be between 0 and 1."
    }
  ]
}
```

**Allowed Enum Values:**
- `clinicType`: `PrimaryCare`, `Dental`, `PT`, `Behavioral`, `Other`
- `appointmentType`: `NewPatient`, `FollowUp`, `Procedure`, `Lab`, `Other`
- `dayOfWeek`: `Monday`, `Tuesday`, `Wednesday`, `Thursday`, `Friday`, `Saturday`, `Sunday`
- `timeBlock`: `Morning`, `Midday`, `Afternoon`, `Evening`

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

11. Example Payloads

11.1 Analyze Request - Small Clinic Schedule

```json
{
  "clinicProfile": {
    "clinicType": "PrimaryCare",
    "providersPerBlock": 2,
    "avgServiceMinutes": 20,
    "timeBlockMinutes": 240
  },
  "appointments": [
    {
      "appointmentId": 101,
      "appointmentType": "NewPatient",
      "dayOfWeek": "Monday",
      "timeBlock": "Morning",
      "leadTimeDays": 7,
      "isNewPatient": true,
      "historicalNoShowRate": 0.25,
      "unitRevenue": 150.0
    },
    {
      "appointmentId": 102,
      "appointmentType": "FollowUp",
      "dayOfWeek": "Monday",
      "timeBlock": "Morning",
      "leadTimeDays": 14,
      "isNewPatient": false,
      "historicalNoShowRate": 0.15,
      "unitRevenue": 100.0
    },
    {
      "appointmentId": 103,
      "appointmentType": "FollowUp",
      "dayOfWeek": "Tuesday",
      "timeBlock": "Afternoon",
      "leadTimeDays": 21,
      "isNewPatient": false,
      "historicalNoShowRate": 0.12,
      "unitRevenue": 100.0
    }
  ]
}
```

11.2 Simulate Request - Overbooking Strategy

```json
{
  "clinicProfile": {
    "clinicType": "Dental",
    "providersPerBlock": 3,
    "avgServiceMinutes": 30,
    "timeBlockMinutes": 240
  },
  "appointments": [
    {
      "appointmentId": 201,
      "appointmentType": "Procedure",
      "dayOfWeek": "Wednesday",
      "timeBlock": "Morning",
      "leadTimeDays": 10,
      "isNewPatient": false,
      "historicalNoShowRate": 0.20,
      "unitRevenue": 250.0
    },
    {
      "appointmentId": 202,
      "appointmentType": "FollowUp",
      "dayOfWeek": "Wednesday",
      "timeBlock": "Morning",
      "leadTimeDays": 14,
      "isNewPatient": false,
      "historicalNoShowRate": 0.15,
      "unitRevenue": 120.0
    }
  ],
  "strategy": {
    "name": "Wednesday Morning Boost",
    "overbookAdditionsBySegment": {
      "Wednesday-Morning": 4
    }
  }
}
```

11.3 Analyze Response - With Segments

```json
{
  "summary": {
    "expectedKeptAppointments": 38.5,
    "baselineRevenue": 4620.0,
    "blockCapacityAppointments": 24.0,
    "expectedOverflowAppointments": 14.5,
    "isOverCapacity": true
  },
  "slotPredictions": [
    {
      "appointmentId": 101,
      "noShowProbability": 0.25
    },
    {
      "appointmentId": 102,
      "noShowProbability": 0.15
    },
    {
      "appointmentId": 103,
      "noShowProbability": 0.12
    }
  ],
  "segments": [
    {
      "segmentKey": "Monday-Morning",
      "expectedKeptAppointments": 15.0,
      "baselineRevenue": 1800.0,
      "capacityAppointments": 24.0,
      "expectedOverflowAppointments": 0.0,
      "isOverCapacity": false
    },
    {
      "segmentKey": "Tuesday-Afternoon",
      "expectedKeptAppointments": 23.5,
      "baselineRevenue": 2820.0,
      "capacityAppointments": 24.0,
      "expectedOverflowAppointments": 0.0,
      "isOverCapacity": false
    }
  ]
}
```

12. Deliverables

- ASP.NET Core Web API
- DTOs + validation layer
- ML inference client (Phase B)
- C engine bindings (Phase C)
- Rate limiting middleware
- Demo-safe configuration
- OpenAPI specification (see `src/shared/contracts/openapi.yaml`)