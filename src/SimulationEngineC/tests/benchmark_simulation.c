#include <stdio.h>
#include <stdlib.h>
#include <time.h>
#include "simulation.h"

// Simple timing helper
static double get_time_ms(void) {
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return ts.tv_sec * 1000.0 + ts.tv_nsec / 1000000.0;
}

static void benchmark_single_simulation(uint32_t n_appts, uint32_t num_trials) {
    SimConfig cfg = {
        .providers_per_block = 2,
        .avg_service_minutes = 15,
        .time_block_minutes = 240,
        .num_trials = num_trials,
        .random_seed = 12345
    };

    Appt* appts = (Appt*)malloc(sizeof(Appt) * n_appts);
    for (uint32_t i = 0; i < n_appts; i++) {
        appts[i] = (Appt){
            .p_no_show = 0.15f + (i % 10) * 0.02f,  // Vary probabilities
            .unit_revenue = 100.0f + (i % 5) * 20.0f,
            .service_minutes = 15,
            .segment_id = (uint8_t)(i % 4)
        };
    }

    SimResult result;
    double start = get_time_ms();
    
    SimStatus st = run_simulation(appts, n_appts, &cfg, &result);
    
    double elapsed = get_time_ms() - start;
    
    if (st == SIM_OK) {
        printf("  %4u appts, %5u trials: %6.2f ms (%.2f μs/trial)\n",
               n_appts, num_trials, elapsed, elapsed * 1000.0 / num_trials);
    } else {
        printf("  ERROR: Simulation failed with status %d\n", (int)st);
    }

    free(appts);
}

static void benchmark_batch_simulation(uint32_t n_appts, uint32_t num_trials, uint32_t n_strategies) {
    SimConfig cfg = {
        .providers_per_block = 2,
        .avg_service_minutes = 15,
        .time_block_minutes = 240,
        .num_trials = num_trials,
        .random_seed = 12345
    };

    Appt* appts = (Appt*)malloc(sizeof(Appt) * n_appts);
    for (uint32_t i = 0; i < n_appts; i++) {
        appts[i] = (Appt){
            .p_no_show = 0.15f,
            .unit_revenue = 100.0f,
            .service_minutes = 15,
            .segment_id = (uint8_t)(i % 4)
        };
    }

    uint16_t* strategies = (uint16_t*)malloc(sizeof(uint16_t) * n_strategies * 4);
    SimResult* results = (SimResult*)malloc(sizeof(SimResult) * n_strategies);
    
    // Create strategies with varying overbooking
    for (uint32_t s = 0; s < n_strategies; s++) {
        for (uint32_t seg = 0; seg < 4; seg++) {
            strategies[s * 4 + seg] = (uint16_t)(s * 2);  // Increasing overbooking
        }
    }

    double start = get_time_ms();
    
    SimStatus st = run_simulation_batch(appts, n_appts, &cfg, strategies, n_strategies, 4, results);
    
    double elapsed = get_time_ms() - start;
    
    if (st == SIM_OK) {
        printf("  %4u appts, %5u trials, %3u strategies: %6.2f ms (%.2f ms/strategy)\n",
               n_appts, num_trials, n_strategies, elapsed, elapsed / n_strategies);
    } else {
        printf("  ERROR: Batch simulation failed with status %d\n", (int)st);
    }

    free(appts);
    free(strategies);
    free(results);
}

int main(void) {
    printf("=== Simulation Engine Benchmarks ===\n\n");
    
    printf("Single Simulation Performance:\n");
    benchmark_single_simulation(10, 1000);
    benchmark_single_simulation(50, 1000);
    benchmark_single_simulation(100, 1000);
    benchmark_single_simulation(50, 5000);
    benchmark_single_simulation(50, 10000);
    benchmark_single_simulation(100, 10000);
    
    printf("\nBatch Simulation Performance:\n");
    benchmark_batch_simulation(50, 5000, 10);
    benchmark_batch_simulation(50, 5000, 50);
    benchmark_batch_simulation(50, 5000, 100);
    benchmark_batch_simulation(100, 10000, 50);
    
    printf("\nTarget: <100ms for 10,000 trials × 1 schedule\n");
    printf("Benchmark complete.\n");
    
    return 0;
}

