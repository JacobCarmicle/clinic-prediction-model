This section answers the most important practical question:

“Can I actually deploy this for free and let people use it?”
Yes — cleanly and credibly.

1. Deployment Goals

The deployment strategy is designed to:

support a free, anonymous public demo

run on CPU-only infrastructure

minimize operational cost

remain HIPAA-safe

scale down just as well as it scales up

2. High-Level Deployment Architecture
User (Browser / API Client)
        ↓
ASP.NET Core API (Public Endpoint)
        ↓
ML Inference (Local or Sidecar)
        ↓
C Simulation Engine (Native Library)


Key idea:

One primary service (the .NET API)

ML + C are embedded or sidecar components, not separate paid services

3. Recommended Hosting (Free / Near-Free)
Option A — Azure (Best Resume Alignment)

Azure App Service (Free / Basic tier)

Azure Container Apps (low-cost, scalable)

Azure Container Registry (free tier for small images)

Pros:

Enterprise credibility

Matches your existing Azure experience

Easy CI/CD later

Option B — Fly.io / Render / Railway

Container-based

Free tier sufficient for demo traffic

Pros:

Extremely simple deployment

Low ops overhead

4. Container Strategy
Single Container (Recommended for Demo)
Container
├── ASP.NET Core API
├── ML model artifacts
└── simulation.so / simulation.dll


Benefits:

simpler networking

no service-to-service latency

easier free-tier deployment

5. ML Deployment Options
Option 1 — In-Process Inference (Preferred)

Model exported to ONNX

Loaded at API startup

CPU inference via ONNX Runtime

Pros:

fast

cheap

no extra services

Option 2 — Python Sidecar (Optional)

FastAPI service

Used only if training/inference pipeline requires Python

Used mainly in private deployments.

6. C Simulation Integration

Compiled as shared library:

Linux: simulation.so

Windows: simulation.dll

Loaded via P/Invoke

No runtime dependencies

7. Demo Mode Configuration
Hard Limits

max appointments per request (e.g., 200)

max strategies per optimization call

fixed Monte Carlo trials (e.g., 5,000)

request timeout caps

Soft Limits

warnings for large schedules

reduced confidence messaging

8. Demo Dataset

Provide:

synthetic clinic schedules

realistic no-show distributions

multiple specialties

Users can:

download sample CSV

upload modified versions

experiment without risk

9. CI/CD (Optional but Impressive)

Basic pipeline:

GitHub Push
  ↓
Build + Test
  ↓
Build Container
  ↓
Deploy to Free Tier


This is resume gold if included.

10. Observability (Minimal)

Track:

request counts

latency

error rates

simulation time

No PII, no raw data logging.

11. Cost Profile (Demo)
Component	Cost
API hosting	$0–$5
Storage	$0
Inference	CPU-only
Simulation	Native C

This is intentionally cheap.

12. Failure & Fallback Modes

ML unavailable → fallback to historical rate baseline

Simulation error → return partial metrics + warning

Traffic spike → rate limiting kicks in

13. Deliverables

Dockerfile

Deployment config

Demo dataset

Usage limits

Environment-based config (demo vs private)