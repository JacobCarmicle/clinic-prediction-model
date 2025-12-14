1. Purpose

The simulation engine evaluates schedule outcomes under uncertainty. It consumes per-appointment no-show probabilities from the ML layer and runs fast Monte Carlo simulations to estimate:

- expected kept appointments
- overload probability (demand > capacity)
- expected wait time / queue delay
- expected revenue recovered
- distributions (not just point estimates)

This is the system’s differentiator: clinics don’t just get predictions — they get policy simulation.

2. Inputs
2.1 Simulation Request (logical)

For each appointment slot i:

- p_i: no-show probability (0–1)
- service_minutes_i (optional; default from clinic profile)
- unit_revenue_i (optional)

Global parameters:

- providers_per_block
- avg_service_minutes (fallback if per-slot missing)
- time_block_minutes (e.g., 240 for a morning block)
- num_trials (Monte Carlo iterations)
- random_seed (for reproducibility)

2.2 Overbooking Strategy

Defines how many extra appointments are added per segment:

Segments are defined by:

- day_of_week
- time_block
- appointment_type (optional)

Example policy:

- Tue/Morning: +15%
- All other blocks: +0%

3. Outputs

Per strategy, return:

- expected_kept_appointments
- overload_probability
- expected_wait_minutes
- p95_wait_minutes
- revenue_recovered
- kept_distribution (optional histogram summary)

Example:

```json
{
  "expected_kept_appointments": 42.3,
  "overload_probability": 0.04,
  "expected_wait_minutes": 7.1,
  "p95_wait_minutes": 18.6,
  "revenue_recovered": 510.0
}
```

4. Simulation Model
4.1 Attendance Sampling

For each trial:

For each appointment i, sample:

attend_i ~ Bernoulli(1 - p_i)

4.2 Capacity Model

For a time block:

capacity per block = (providers_per_block * time_block_minutes) / avg_service_minutes

Overload occurs when:

kept_appointments > capacity

4.3 Wait Time Model (Queue Approximation)

MVP option (fast + simple):

if kept <= capacity: wait ≈ 0–small baseline

if kept > capacity: wait grows proportional to overflow:

overflow = kept - capacity

avg_wait ≈ overflow * avg_service_minutes / providers_per_block

Advanced option (later):

stochastic arrivals within block + M/M/c approximation or discrete-event simulation

5. Monte Carlo Configuration

Recommended defaults (public demo):

num_trials: 5,000 (fast, stable estimates)

random_seed: fixed unless user requests randomness

Private deployment:

10,000–50,000 trials per strategy

6. Performance Requirements

Must simulate thousands of strategies quickly

Goal: 10,000 trials × 1 schedule in under ~100ms on commodity CPU (ballpark target)

Key optimizations:

- tight loops, stack arrays where possible
- SIMD-friendly Bernoulli sampling (optional)
- precomputed segment groupings
- avoid heap allocations inside trials

7. Engine API (C Interface)

7.1 C Header Sketch

```c
// simulation.h

#pragma once
#include <stdint.h>

typedef struct {
  float p_no_show;
  float unit_revenue;
  uint16_t service_minutes; // optional, else 0
  uint8_t segment_id;       // precomputed grouping
} Appt;

typedef struct {
  uint16_t providers_per_block;
  uint16_t avg_service_minutes;
  uint16_t time_block_minutes;
  uint32_t num_trials;
  uint32_t random_seed;
} SimConfig;

typedef struct {
  float expected_kept;
  float overload_probability;
  float expected_wait_minutes;
  float p95_wait_minutes;
  float revenue_recovered;
} SimResult;

int run_simulation(const Appt* appts, uint32_t n_appts,
                   const SimConfig* cfg,
                   SimResult* out_result);
```

8. Strategy Evaluation (Batch Mode)

To support optimization, the C layer should support evaluating multiple strategies efficiently.

Two options:

**Option A (Simpler)**

.NET loop calls C once per strategy.

**Option B (Faster, preferred)**

C accepts a list of strategies and returns an array of results:

```c
int run_simulation_batch(const Appt* appts, uint32_t n_appts,
                         const SimConfig* cfg,
                         const uint16_t* strategy_overbook_additions, // per segment
                         uint32_t n_strategies,
                         SimResult* out_results);
```

9. Reproducibility

- If random_seed is fixed, results are deterministic
- PRNG: PCG32 or xorshift32 (fast, consistent)

10. Error Handling

Return codes:

- 0 success
- 1 invalid config
- 2 invalid probabilities
- 3 empty schedule
- 4 overflow / internal error

11. Testing Plan

**Unit Tests**

- Bernoulli sampling correctness
- capacity calculation
- overload probability sanity checks (p=0 vs p=1 extremes)

**Property Tests**

- Increasing overbooking should not reduce overload probability (monotonic expectation)

**Benchmarks**

- speed vs number of appointments and trials

12. Deliverables

- simulation.h / simulation.c
- deterministic PRNG
- batch strategy support (preferred)
- unit tests + benchmark harness
- simple CLI runner for debugging (optional)