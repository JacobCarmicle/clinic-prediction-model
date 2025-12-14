1. Objective

The ML component predicts the probability that a scheduled appointment results in a no-show using anonymized, operational features. The model’s output feeds directly into the simulation engine to evaluate scheduling strategies.

2. Target Variable
Primary Target
no_show ∈ {0, 1}


1 → appointment was a no-show

0 → appointment was kept

Model Output
P(no_show = 1 | features)

3. Modeling Strategy
3.1 Baseline Model (MVP)

**Logistic Regression**

- Interpretable
- Fast to train
- CPU-friendly
- Strong baseline for binary outcomes

Used to:

- Validate feature usefulness
- Establish baseline metrics
- Provide explainability early

3.2 Advanced Model (Production)

**Gradient Boosted Trees (XGBoost / LightGBM)**

Why:

- Handles nonlinear interactions
- Strong performance on tabular data
- Robust to mixed feature types
- Easy to constrain complexity

4. Feature Encoding

**Categorical Features**

- One-hot encoding (baseline)
- Target encoding (advanced)

**Numeric Features**

- Lead time
- Historical no-show rate
- Revenue (optional)
- Standardized or log-transformed if skewed

**Boolean Features**

- Binary (0/1)

5. Training Data

**Data Sources**

- Synthetic clinic datasets
- Open operational scheduling datasets (where available)
- Aggregated historical clinic exports (private deployments)

**Training Granularity**

- One row per appointment slot
- No patient identifiers retained

6. Train / Validation Strategy
Temporal Split

Train on earlier data

Validate on later periods

Prevents leakage and mirrors real-world use.

7. Evaluation Metrics

| Metric | Purpose |
|--------|---------|
| ROC-AUC | Ranking ability |
| Log Loss | Probability calibration |
| Brier Score | Forecast accuracy |
| Calibration Curve | Over/under-confidence |
| Precision @ High Risk | Operational focus |
8. Calibration & Confidence
Probability Calibration

Platt Scaling or Isotonic Regression

Confidence Intervals

Bootstrap resampling

Quantile predictions (GBDT)

Output example:

"confidence_interval": [0.21, 0.33]

9. Explainability

**Feature Importance**

- Gain-based importance (GBDT)
- Coefficients (logistic)

**Local Explanations**

- SHAP values (optional)
- Top drivers per prediction

10. Drift Detection

The system monitors:

Feature distribution drift (PSI)

Prediction drift

Outcome drift (when labels available)

Warnings surfaced to users:

“Model confidence reduced due to seasonal drift.”

11. Inference Performance

CPU-only inference

Sub-millisecond per record

Batch-friendly for schedules

12. Model Artifacts

Stored artifacts:

- Trained model file
- Feature schema hash
- Calibration parameters
- Training metadata

13. Failure Modes & Safeguards

| Risk | Mitigation |
|------|------------|
| Sparse data | Fallback to baseline |
| Out-of-distribution inputs | Confidence penalty |
| Concept drift | Warning + retrain flag |
14. Deliverables

- Trained baseline model
- Advanced GBDT model
- Evaluation report
- Explainability outputs
- Inference interface