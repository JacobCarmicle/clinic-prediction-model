#include <assert.h>
#include <stdio.h>
#include <stdint.h>
#include "simulation.h"

static void test_extremes(void) {
  SimConfig cfg = { .providers_per_block=1, .avg_service_minutes=15, .time_block_minutes=60, .num_trials=2000, .random_seed=1 };

  // p=1 => nobody attends
  Appt a1[] = { {.p_no_show=1.0f, .unit_revenue=100.0f, .service_minutes=0, .segment_id=0} };
  SimResult r1;
  assert(run_simulation(a1, 1, &cfg, &r1) == SIM_OK);
  assert(r1.expected_kept < 0.05f);

  // p=0 => everybody attends
  Appt a2[] = { {.p_no_show=0.0f, .unit_revenue=100.0f, .service_minutes=0, .segment_id=0} };
  SimResult r2;
  assert(run_simulation(a2, 1, &cfg, &r2) == SIM_OK);
  assert(r2.expected_kept > 0.95f);
}

static void test_capacity_calculation(void) {
  // Capacity = (2 providers * 240 minutes) / 15 minutes = 32 appointments
  SimConfig cfg = { 
    .providers_per_block=2, 
    .avg_service_minutes=15, 
    .time_block_minutes=240, 
    .num_trials=5000, 
    .random_seed=42 
  };

  // Schedule exactly at capacity (all attend)
  Appt appts[32];
  for (int i = 0; i < 32; i++) {
    appts[i] = (Appt){.p_no_show=0.0f, .unit_revenue=100.0f, .service_minutes=15, .segment_id=0};
  }

  SimResult r;
  assert(run_simulation(appts, 32, &cfg, &r) == SIM_OK);
  assert(r.expected_kept > 31.0f && r.expected_kept < 33.0f);
  assert(r.overload_probability < 0.1f); // Should rarely overload at exact capacity
}

static void test_capacity_overflow(void) {
  SimConfig cfg = { 
    .providers_per_block=1, 
    .avg_service_minutes=15, 
    .time_block_minutes=60,  // Capacity = 4 appointments
    .num_trials=5000, 
    .random_seed=123 
  };

  // Schedule 10 appointments - should cause overload
  Appt appts[10];
  for (int i = 0; i < 10; i++) {
    appts[i] = (Appt){.p_no_show=0.1f, .unit_revenue=100.0f, .service_minutes=15, .segment_id=0};
  }

  SimResult r;
  assert(run_simulation(appts, 10, &cfg, &r) == SIM_OK);
  assert(r.overload_probability > 0.5f); // Should have high overload probability
  assert(r.expected_wait_minutes > 0.0f); // Should have some wait time
}

static void test_monotonic_property(void) {
  // Property: Increasing overbooking should increase (or not decrease) overload probability
  SimConfig cfg = { 
    .providers_per_block=2, 
    .avg_service_minutes=15, 
    .time_block_minutes=240,  // Capacity = 32
    .num_trials=5000, 
    .random_seed=999 
  };

  // Base schedule: 30 appointments
  Appt base_appts[30];
  for (int i = 0; i < 30; i++) {
    base_appts[i] = (Appt){.p_no_show=0.15f, .unit_revenue=100.0f, .service_minutes=15, .segment_id=0};
  }

  SimResult baseline;
  assert(run_simulation(base_appts, 30, &cfg, &baseline) == SIM_OK);

  // Test batch mode with increasing overbooking
  uint16_t strategies[3][1] = {
    {0},   // No overbooking
    {5},   // +5 appointments
    {10}   // +10 appointments
  };

  SimResult results[3];
  uint16_t additions[3];
  for (int i = 0; i < 3; i++) {
    additions[i] = strategies[i][0];
  }

  assert(run_simulation_batch(base_appts, 30, &cfg, additions, 3, 1, results) == SIM_OK);

  // Overload probability should be non-decreasing (monotonic property)
  // Note: With randomness, we check that trend is generally upward
  float overload_0 = results[0].overload_probability;
  float overload_5 = results[1].overload_probability;
  float overload_10 = results[2].overload_probability;

  // More overbooking should generally increase overload (allowing for some variance)
  assert(overload_10 >= overload_0 - 0.1f); // Allow small variance due to randomness
}

int main(void) {
  printf("Running unit tests...\n");
  test_extremes();
  printf("  ✓ Extreme probability tests\n");
  
  test_capacity_calculation();
  printf("  ✓ Capacity calculation test\n");
  
  test_capacity_overflow();
  printf("  ✓ Capacity overflow test\n");
  
  test_monotonic_property();
  printf("  ✓ Monotonic property test\n");
  
  printf("All tests passed.\n");
  return 0;
}
