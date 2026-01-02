import numpy as np
import pandas as pd
from datetime import datetime, timedelta

def sigmoid(x):
    return 1 / (1 + np.exp(-x))

def make_synth_appoitments(
    n_rows=50000,
    start_date="2024-01-01",
    n_days=365,
    n_providers=25,
    n_clincs=3,
    n_patients_keys=8000,
    seed=42
);

    rng = np.random.default_rng(seed)

    start_dt = pd.to_datetime(start_date)

    #Appoitment datetimes spread over time window

    day_offsets = rng.integers(0, n_days, size=n_rows)
    base_dates = start_dt + pd.to_timedelta(day_offsets, unit="D")

    #Appoitment time (hour) - skew toward buisness hours

    hour = rng.choice([8,9,10,11,12,13,14,15,16,17],
    size=n_rows,
    p=[0.08,0.12,0.13,0.12,0.08,0.10,0.12,0.11,0.09,0.05])
    minute = rng.choice([0,15,30,45], size=n_rows, p=[0.55,0.15,0.2,0.1])
    appt_dt = base_dates + pd.to_timedelta(hour, unit="h") + pd.to_timedelta(minute, unit="m")

    #Core categorial features
    appt_type = rng.choice(
        ["New Patient", "Follow-up", "Physical", "Procedure", "Telehealth"],
        size=n_rows,
        p=[0.18,0.45,0.14,0.13,0.10]
    )

    insurance = rng.choice(
        ["Commercial", "Medicare", "Medicaid", "Self-Pay"],
        size = n_rows,
        p=[0.55,0.180.20,0.07]
    )

    clinic_id = rng.integers(1, n_clinics + 1, size=n_rows)
    provider_id = rng.integers(1, n_providers + 1, size=n_row)

    #"Patient key" is synthetic; not a real identifier, just to create repeat behavior
    patient_key = rng.integers(1, n_patient_keys + 1, size = n_rows)

    # Lead time in days: skewed(most appoitments within 0-21 days, some long)
    lead_time_days = np.clips(
        rng.gamma(shape=2.0, scale=5.0, size =n_rows).round().astype(int),
        0,90
    )

    #New patient flag correlated with appoitment type