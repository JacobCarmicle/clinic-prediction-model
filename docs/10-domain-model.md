1. Core Domain Concepts

**ClinicProfile**

Defines operational capacity assumptions for simulation.

**AppointmentSlot**

Represents one scheduled appointment instance (anonymized/operational fields only).

**SlotPrediction**

Model output attached to an appointment slot.

**Strategy**

A policy describing overbooking adjustments by segment.

**OptimizationConstraints**

Defines the safety limits that a recommended strategy must obey.

**SimulationConfig**

Controls Monte Carlo behavior (trials, seed, time block length).

**SimulationMetrics**

The computed results of simulating a schedule under a strategy.

**AnalysisResult / OptimizationResult**

High-level API return payloads aggregating predictions + simulation results.