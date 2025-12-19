#include "simulation.h"
#include "prng.h"
#include <stddef.h>
#include <stdlib.h>
#include <string.h>

static SimStatus validate_inputs(const Appt* appts, uint32_t n_appts, const SimConfig* cfg, SimResult* out){
    if(!cfg || !out) return SIM_ERR_INVALID_CONFIG;
    if(!appts || n_appts == 0) return SIM_ERR_EMPTY_SCHEDULE;

    if(cfg->providers_per_block == 0 || cfg->avg_service_minutes == 0 || cfg->time_block_minutes == 0 ||cfg->num_trials ==0)
    return SIM_ERR_INVALID_CONFIG;

    for(uint32_t i = 0; i < n_appts; i++) {
        float p = appts[i].p_no_show;
        if(!(p >= 0.0f && p <= 1.0f)) return SIM_ERR_INVALID_PROB;
        if (appts[i].unit_revenue < 0.0f) return SIM_ERR_INVALID_CONFIG;
    }
    return SIM_OK;
}

static inline float capacity_appointments(const SimConfig* cfg){
    //capacity = (providers * block_minutes) / avg_service_minutes
    return ((float)cfg->providers_per_block * (float)cfg->time_block_minutes) / (float)cfg->avg_service_minutes;
}

static inline float wait_minutes_approx(float kept, float cap, const SimConfig* cfg) {
    if (kept <= cap) return 0.0f;
    float overflow = kept - cap;
    return overflow * (float)cfg->avg_service_minutes / (float)cfg->providers_per_block;
  }
  
  static float percentile95_from_hist(const uint32_t* hist, uint32_t hist_len, uint32_t total_count) {
    if (total_count == 0) return 0.0f;
    uint32_t target = (uint32_t)((0.95f * (float)total_count) + 0.5f);
    if (target == 0) target = 1;
  
    uint32_t cum = 0;
    for (uint32_t i = 0; i < hist_len; i++) {
      cum += hist[i];
      if (cum >= target) return (float)i;
    }
    return (float)(hist_len - 1);
  }
  
  SimStatus run_simulation(
    const Appt* appts, uint32_t n_appts,
    const SimConfig* cfg,
    SimResult* out_result
  ) {
    SimStatus st = validate_inputs(appts, n_appts, cfg, out_result);
    if (st != SIM_OK) return st;
  
    memset(out_result, 0, sizeof(*out_result));
  
    const float cap = capacity_appointments(cfg);
  
    // Wait histogram in whole minutes, capped (simple + demo-safe)
    // If your waits can exceed this, we clamp to the last bucket.
    enum { WAIT_HIST_MAX_MIN = 240 };
    uint32_t wait_hist[WAIT_HIST_MAX_MIN + 1];
    memset(wait_hist, 0, sizeof(wait_hist));
  
    XorShift32 rng;
    prng_seed(&rng, cfg->random_seed);
  
    float kept_sum = 0.0f;
    float wait_sum = 0.0f;
    float rev_sum  = 0.0f;
    uint32_t overload_count = 0;
  
    for (uint32_t t = 0; t < cfg->num_trials; t++) {
      float kept = 0.0f;
      float rev  = 0.0f;
  
      for (uint32_t i = 0; i < n_appts; i++) {
        float attend_p = 1.0f - appts[i].p_no_show;
        float u = prng_next_f01(&rng);
        if (u < attend_p) {
          kept += 1.0f;
          rev  += appts[i].unit_revenue;
        }
      }
  
      if (kept > cap) overload_count++;
  
      float wait = wait_minutes_approx(kept, cap, cfg);
  
      // bucketize wait to whole minutes for p95
      uint32_t w = (uint32_t)(wait + 0.5f);
      if (w > WAIT_HIST_MAX_MIN) w = WAIT_HIST_MAX_MIN;
      wait_hist[w]++;
  
      kept_sum += kept;
      wait_sum += wait;
      rev_sum  += rev;
    }
  
    float trials = (float)cfg->num_trials;
    out_result->expected_kept = kept_sum / trials;
    out_result->overload_probability = (float)overload_count / trials;
    out_result->expected_wait_minutes = wait_sum / trials;
    out_result->p95_wait_minutes = percentile95_from_hist(wait_hist, WAIT_HIST_MAX_MIN + 1, cfg->num_trials);
    out_result->revenue_recovered = rev_sum / trials;
  
    return SIM_OK;
  }
  
  SimStatus run_simulation_batch(
    const Appt* appts, uint32_t n_appts,
    const SimConfig* cfg,
    const uint16_t* strategy_overbook_additions,
    uint32_t n_strategies,
    uint16_t n_segments,
    SimResult* out_results
  ) {
    if (!out_results) return SIM_ERR_INVALID_CONFIG;
    if (!strategy_overbook_additions && n_strategies > 0) return SIM_ERR_INVALID_CONFIG;
  
    SimStatus st = validate_inputs(appts, n_appts, cfg, &(SimResult){0});
    if (st != SIM_OK) return st;
  
    // For MVP: interpret “additions” as extra *synthetic* appointments per segment.
    // We approximate their p_no_show and revenue by averaging that segment’s existing appts.
    // (Later: .NET can materialize explicit extra slots with chosen properties.)
    float* seg_avg_p = NULL;
    float* seg_avg_rev = NULL;
    uint32_t* seg_counts = NULL;
  
    // Small stack fallback if n_segments is small, else bail for MVP (keeps code simple + safe).
    if (n_segments == 0) return SIM_ERR_INVALID_CONFIG;
    if (n_segments > 2048) return SIM_ERR_INVALID_CONFIG;
  
    seg_avg_p = (float*)malloc(sizeof(float) * n_segments);
    seg_avg_rev = (float*)malloc(sizeof(float) * n_segments);
    seg_counts = (uint32_t*)malloc(sizeof(uint32_t) * n_segments);
    if (!seg_avg_p || !seg_avg_rev || !seg_counts) {
      free(seg_avg_p); free(seg_avg_rev); free(seg_counts);
      return SIM_ERR_INTERNAL;
    }
  
    for (uint16_t s = 0; s < n_segments; s++) {
      seg_avg_p[s] = 0.0f;
      seg_avg_rev[s] = 0.0f;
      seg_counts[s] = 0;
    }
  
    for (uint32_t i = 0; i < n_appts; i++) {
      uint16_t sid = appts[i].segment_id;
      if (sid >= n_segments) { free(seg_avg_p); free(seg_avg_rev); free(seg_counts); return SIM_ERR_INVALID_CONFIG; }
      seg_avg_p[sid] += appts[i].p_no_show;
      seg_avg_rev[sid] += appts[i].unit_revenue;
      seg_counts[sid] += 1;
    }
  
    for (uint16_t sid = 0; sid < n_segments; sid++) {
      if (seg_counts[sid] > 0) {
        seg_avg_p[sid] /= (float)seg_counts[sid];
        seg_avg_rev[sid] /= (float)seg_counts[sid];
      } else {
        // no appts in segment => neutral defaults
        seg_avg_p[sid] = 0.25f;
        seg_avg_rev[sid] = 0.0f;
      }
    }
  
    // Evaluate each strategy by creating an “effective schedule” in-loop (no heap per trial).
    for (uint32_t k = 0; k < n_strategies; k++) {
      // Strategy-specific RNG seed: stable across ordering
      SimConfig local = *cfg;
      local.random_seed = cfg->random_seed ^ (0x9E3779B9u * (k + 1u));
  
      // Simulate by extending the inner loop with extra segment additions
      // without physically allocating new appointments.
      SimResult r = {0};
  
      const float cap = capacity_appointments(&local);
  
      enum { WAIT_HIST_MAX_MIN = 240 };
      uint32_t wait_hist[WAIT_HIST_MAX_MIN + 1];
      memset(wait_hist, 0, sizeof(wait_hist));
  
      XorShift32 rng;
      prng_seed(&rng, local.random_seed);
  
      float kept_sum = 0.0f;
      float wait_sum = 0.0f;
      float rev_sum  = 0.0f;
      uint32_t overload_count = 0;
  
      const uint16_t* adds = strategy_overbook_additions + (k * (uint32_t)n_segments);
  
      for (uint32_t t = 0; t < local.num_trials; t++) {
        float kept = 0.0f;
        float rev  = 0.0f;
  
        // original appts
        for (uint32_t i = 0; i < n_appts; i++) {
          float attend_p = 1.0f - appts[i].p_no_show;
          float u = prng_next_f01(&rng);
          if (u < attend_p) {
            kept += 1.0f;
            rev  += appts[i].unit_revenue;
          }
        }
  
        // synthetic added appts per segment
        for (uint16_t sid = 0; sid < n_segments; sid++) {
          uint16_t extra = adds[sid];
          if (extra == 0) continue;
  
          float attend_p = 1.0f - seg_avg_p[sid];
          float unit_rev = seg_avg_rev[sid];
  
          for (uint16_t e = 0; e < extra; e++) {
            float u = prng_next_f01(&rng);
            if (u < attend_p) {
              kept += 1.0f;
              rev  += unit_rev;
            }
          }
        }
  
        if (kept > cap) overload_count++;
        float wait = wait_minutes_approx(kept, cap, &local);
  
        uint32_t w = (uint32_t)(wait + 0.5f);
        if (w > WAIT_HIST_MAX_MIN) w = WAIT_HIST_MAX_MIN;
        wait_hist[w]++;
  
        kept_sum += kept;
        wait_sum += wait;
        rev_sum  += rev;
      }
  
      float trials = (float)local.num_trials;
      r.expected_kept = kept_sum / trials;
      r.overload_probability = (float)overload_count / trials;
      r.expected_wait_minutes = wait_sum / trials;
      r.p95_wait_minutes = percentile95_from_hist(wait_hist, WAIT_HIST_MAX_MIN + 1, local.num_trials);
      r.revenue_recovered = rev_sum / trials;
  
      out_results[k] = r;
    }
  
    free(seg_avg_p);
    free(seg_avg_rev);
    free(seg_counts);
    return SIM_OK;
  }

  //Note: this batch MVP uses “synthetic added appointments per segment” (averaging segment p/revenue). It’s fast and good enough for your Phase C demo; later .NET can explicitly materialize added appointments for more realism. 