#include <stdio.h>
#include <stdlib.h>
#include "simulation.h"

int main(void) {
  Appt appts[] = {
    {.p_no_show = 0.20f, .unit_revenue = 120.0f, .service_minutes = 0, .segment_id = 0},
    {.p_no_show = 0.10f, .unit_revenue = 100.0f, .service_minutes = 0, .segment_id = 0},
    {.p_no_show = 0.35f, .unit_revenue = 150.0f, .service_minutes = 0, .segment_id = 1},
  };

  SimConfig cfg = {
    .providers_per_block = 2,
    .avg_service_minutes = 15,
    .time_block_minutes = 240,
    .num_trials = 5000,
    .random_seed = 12345
  };

  SimResult r;
  SimStatus st = run_simulation(appts, 3, &cfg, &r);
  if (st != SIM_OK) {
    fprintf(stderr, "Simulation failed: %d\n", (int)st);
    return 1;
  }

  printf("expected_kept=%.3f\n", r.expected_kept);
  printf("overload_prob=%.3f\n", r.overload_probability);
  printf("expected_wait=%.3f\n", r.expected_wait_minutes);
  printf("p95_wait=%.3f\n", r.p95_wait_minutes);
  printf("revenue_recovered=%.2f\n", r.revenue_recovered);

  return 0;
}
