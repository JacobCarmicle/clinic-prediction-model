import numpy as np
import pandas as pd
from datetime import datetime, timedelta

def sigmoid(x):
    return 1 / (1 + np.exp(-x))

def make_synth_appointments(
    n_rows=50000,
    start_date="2024-01-01",
    n_days=365,
    n_providers=25,
    n_clinics=3,
    n_patients_keys=8000,
    seed=42
):
    """
    Generate synthetic appointment data matching the ClinicFlow data model.
    
    Returns DataFrame with columns:
    - appointment_id: Unique identifier
    - appointment_type: NewPatient, FollowUp, Procedure, Lab, Other
    - day_of_week: Monday-Sunday
    - time_block: Morning, Midday, Afternoon, Evening
    - lead_time_days: Days between booking and appointment
    - is_new_patient: Boolean flag
    - historical_no_show_rate: Historical rate for segment (0-1)
    - unit_revenue: Expected revenue per kept appointment
    - no_show: Target variable for ML training (0=kept, 1=no-show)
    """
    rng = np.random.default_rng(seed)

    start_dt = pd.to_datetime(start_date)

    # Appointment datetimes spread over time window
    day_offsets = rng.integers(0, n_days, size=n_rows)
    base_dates = start_dt + pd.to_timedelta(day_offsets, unit="D")

    # Appointment time (hour) - skew toward business hours
    hour = rng.choice([8,9,10,11,12,13,14,15,16,17],
        size=n_rows,
        p=[0.08,0.12,0.13,0.12,0.08,0.10,0.12,0.11,0.09,0.05])
    minute = rng.choice([0,15,30,45], size=n_rows, p=[0.55,0.15,0.2,0.1])
    appt_dt = base_dates + pd.to_timedelta(hour, unit="h") + pd.to_timedelta(minute, unit="m")

    # Core categorical features - using internal types for generation logic
    # Will map to expected enum values later
    appt_type_internal = rng.choice(
        ["NewPatient", "FollowUp", "Lab", "Procedure", "Other"],
        size=n_rows,
        p=[0.18,0.45,0.14,0.13,0.10]
    )

    # Internal features for generating realistic patterns (not in final output)
    insurance = rng.choice(
        ["Commercial", "Medicare", "Medicaid", "Self-Pay"],
        size=n_rows,
        p=[0.55,0.18,0.20,0.07]
    )

    # Internal IDs for creating correlations (not exported for HIPAA safety)
    clinic_id = rng.integers(1, n_clinics + 1, size=n_rows)
    provider_id = rng.integers(1, n_providers + 1, size=n_rows)
    
    # "Patient key" is synthetic; not a real identifier, just to create repeat behavior
    # Used internally but not exported
    patient_key = rng.integers(1, n_patients_keys + 1, size=n_rows)

    # Lead time in days: skewed (most appointments within 0-21 days, some long)
    lead_time_days = np.clip(
        rng.gamma(shape=2.0, scale=5.0, size=n_rows).round().astype(int),
        0, 90
    )

    # New patient flag correlated with appointment type
    is_new_patient = (appt_type_internal == "NewPatient").astype(int)

    # Day-of-week (0=Monday, 6=Sunday)
    # Convert to Series to use .dt accessor
    appt_dt_series = pd.Series(appt_dt)
    day_of_week_num = appt_dt_series.dt.dayofweek
    month = appt_dt_series.dt.month
    is_weekend = (day_of_week_num >= 5).astype(int)

    # Optional: reminder sent / confirmation (used internally for no-show modeling)
    reminder_sent = rng.binomial(1, 0.88, size=n_rows)
    confirmed = rng.binomial(1, 0.62, size=n_rows) * reminder_sent

    # Provider & clinic effects (some providers have higher no-shows)
    provider_effect = rng.normal(0.0, 0.35, size=n_providers + 1)
    clinic_effect = rng.normal(0.0, 0.6, size=n_clinics + 1)

    # Create a latent "Patient reliability" trait so some patients are repeat no-shows
    patient_trait = rng.normal(0.0, 0.6, size=n_patients_keys + 1)

    # Base log-odds
    logit = -1.6  # ~17% base no-show

    # Add effects (these create learnable structures)
    # Lead time: Longer -> higher no-show
    logit += 0.018 * lead_time_days

    # New patient no-show more
    logit += 0.15 * is_new_patient

    # Afternoon slightly worse
    logit += 0.15 * (hour >= 15)

    # Monday slightly worse
    logit += 0.12 * (day_of_week_num == 0)

    # Appointment type effects
    type_map = {
        "NewPatient": 0.25,
        "FollowUp": -0.10,
        "Lab": -0.05,
        "Procedure": -0.15,
        "Other": 0.10
    }
    logit += pd.Series(appt_type_internal).map(type_map).to_numpy()

    # Insurance effects (small)
    ins_map = {"Commercial": -0.05, "Medicaid": 0.12, "Medicare": 0.02, "Self-Pay": 0.08}
    logit += pd.Series(insurance).map(ins_map).to_numpy()

    # Reminder/confirmation reduce no-show
    logit += -0.25 * reminder_sent
    logit += -0.65 * confirmed

    # Provider/clinic/patient random effects
    logit += provider_effect[provider_id]
    logit += clinic_effect[clinic_id]
    logit += 0.7 * patient_trait[patient_key]

    # Convert to probability & sample label
    p_no_show = sigmoid(logit)
    no_show = rng.binomial(1, p_no_show, size=n_rows)

    # Create intermediate DataFrame for historical calculations
    df_temp = pd.DataFrame({
        "appointment_datetime": pd.Series(appt_dt).astype("datetime64[ns]"),
        "clinic_id": clinic_id,
        "provider_id": provider_id,
        "patient_key": patient_key,
        "appointment_type": appt_type_internal,
        "lead_time_days": lead_time_days,
        "hour": hour,
        "day_of_week_num": day_of_week_num,
        "is_new_patient": is_new_patient,
        "no_show": no_show
    })

    # Sort by time (helps with temporal split + "historical rate" features)
    df_temp = df_temp.sort_values("appointment_datetime").reset_index(drop=True)

    # Historical no-show rate per segment WITHOUT leakage:
    # rolling mean shifted by 1 so it only uses past outcomes
    # Group by appointment type + day of week + time block for segment-level history
    df_temp["time_block"] = df_temp["hour"].apply(lambda h: 
        "Morning" if h < 12 else ("Midday" if h < 14 else ("Afternoon" if h < 17 else "Evening"))
    )
    df_temp["day_of_week"] = df_temp["day_of_week_num"].map({
        0: "Monday", 1: "Tuesday", 2: "Wednesday", 3: "Thursday",
        4: "Friday", 5: "Saturday", 6: "Sunday"
    })
    
    # Create segment key for historical rate calculation
    df_temp["segment_key"] = (
        df_temp["appointment_type"].astype(str) + "_" + 
        df_temp["day_of_week"].astype(str) + "_" + 
        df_temp["time_block"].astype(str)
    )
    
    # Calculate historical no-show rate per segment (shifted to avoid leakage)
    df_temp["historical_no_show_rate"] = (
        df_temp.groupby("segment_key")["no_show"]
          .apply(lambda s: s.shift(1).rolling(50, min_periods=1).mean())
          .reset_index(level=0, drop=True)
    ).fillna(df_temp["no_show"].mean())

    # Generate unit revenue (varies by appointment type)
    revenue_map = {
        "NewPatient": 150.0,
        "FollowUp": 120.0,
        "Lab": 80.0,
        "Procedure": 250.0,
        "Other": 100.0
    }
    base_revenue = pd.Series(appt_type_internal).map(revenue_map).to_numpy()
    # Add some variation (±20%)
    unit_revenue = base_revenue * (1.0 + rng.normal(0.0, 0.15, size=n_rows))
    unit_revenue = np.clip(unit_revenue, 0, None)  # Ensure non-negative

    # Build final DataFrame matching expected schema
    # Include no_show target for ML training (this is the label)
    df = pd.DataFrame({
        "appointment_id": np.arange(1, n_rows + 1),
        "appointment_type": df_temp["appointment_type"].values,
        "day_of_week": df_temp["day_of_week"].values,
        "time_block": df_temp["time_block"].values,
        "lead_time_days": lead_time_days,
        "is_new_patient": is_new_patient.astype(bool),
        "historical_no_show_rate": df_temp["historical_no_show_rate"].values,
        "unit_revenue": unit_revenue,
        "no_show": df_temp["no_show"].values  # Target variable for ML training
    })

    # Reset index to ensure clean ordering
    df = df.reset_index(drop=True)

    return df

if __name__ == "__main__":
    df = make_synth_appointments(n_rows=60000, start_date="2024-01-01", n_days=365)
    df.to_csv("synthetic_appointments.csv", index=False)
    print(df.head())
    print("\nColumn names:", df.columns.tolist())
    print("\nAppointment types:", df["appointment_type"].unique())
    print("\nDay of week values:", df["day_of_week"].unique())
    print("\nTime blocks:", df["time_block"].unique())
    print("\nNo-show rate (from historical_no_show_rate):", df["historical_no_show_rate"].mean())
    print("\nActual no-show rate (target variable):", df["no_show"].mean())
