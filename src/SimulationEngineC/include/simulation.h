#pragma once
#include <stdint.h>

#ifdef __cplusplus
extern "C"{
    #endif

    typedef struct {
        float p_no_show;
        float unit_revenue;
        uint16_t service_minutes;
        uint8_t segment_id;       // precomputed grouping
    } Appt;

typedef struct{
    uint16_t providers_per_block; //>=1
    uint16_t avg_service_minutes; //>=1
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
}SimResult;

typedef enum {
    SIM_OK = 0,
    SIM_ERR_INVALID_CONFIG = 1,
    SIM_ERR_INVALID_PROB = 2,
    SIM_ERR_EMPTY_SCHEDULE = 3,
    SIM_ERR_INTERNAL = 4
}SimStatus;

//Single strategy (baseline)
SimStatus run_simulation(
    const Appt*appts, uint32_t n_appts,
    const SimConfig * cfg,
    SimResult* out_result
);

//Batch strategies: each provides "addition per segment"
// Layout: strategy_overbook_additions[(s * n_segments) + seg]
SimStatus run_simulation_batch(
    const Appt* appts, uint32_t n_appts,
    const SimConfig* cfg,
    const uint16_t* strategy_overbook_additions,
    uint32_t n_strategies,
    uint16_t n_segments,
    SimResult* out_results
);
#ifdef __cplusplus
}
#endif