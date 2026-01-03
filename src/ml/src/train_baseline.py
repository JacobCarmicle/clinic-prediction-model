# logistic regression pipeline
# src/train_baseline.py
"""
Train baseline logistic regression model for no-show prediction.

Example:
    python train_baseline.py --csv data/train.csv --schema config/schema.json --out artifacts
"""
import argparse
import sys
import pandas as pd
from sklearn.linear_model import LogisticRegression

from features import load_schema, schema_hash, validate_df, fit_transform, transform
from split import temporal_split
from evaluate import evaluate_binary
from calibrate import PlattCalibrator  # or IsotonicCalibrator
from artifacts import save_artifacts


def main():
    ap = argparse.ArgumentParser(description="Train baseline logistic regression model")
    ap.add_argument("--csv", required=True, help="Path to training CSV file")
    ap.add_argument("--schema", required=True, help="Path to schema.json file")
    ap.add_argument("--out", default="artifacts", help="Output directory for artifacts (default: artifacts)")
    ap.add_argument("--train_frac", type=float, default=0.8, help="Fraction of data for training (default: 0.8)")
    ap.add_argument("--time_col", default=None, help="Optional time column for temporal split")
    ap.add_argument("--no-scale-numeric", dest="scale_numeric", action="store_false", default=True, help="Disable scaling of numeric features (scaling is enabled by default)")
    args = ap.parse_args()

    # Load and validate data
    try:
    df = pd.read_csv(args.csv)
    except FileNotFoundError:
        print(f"Error: CSV file not found: {args.csv}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"Error loading CSV: {e}", file=sys.stderr)
        sys.exit(1)

    try:
    schema = load_schema(args.schema)
    validate_df(df, schema)
    except Exception as e:
        print(f"Error validating data: {e}", file=sys.stderr)
        sys.exit(1)

    # Check target column exists
    if "no_show" not in df.columns:
        print("Error: Target column 'no_show' not found in CSV", file=sys.stderr)
        sys.exit(1)

    # Temporal split
    train_df, valid_df = temporal_split(df, frac_train=args.train_frac, time_col=args.time_col)

    # Feature engineering
    fb = fit_transform(train_df, scale_numeric=args.scale_numeric, schema_path=args.schema)
    X_train, y_train = fb.X, fb.y
    X_valid = transform(fb.preprocessor, valid_df)
    y_valid = valid_df["no_show"].to_numpy(dtype=int)

    model = LogisticRegression(max_iter=2000, solver="lbfgs")
    model.fit(X_train, y_train)

    # Raw probabilities
    p_valid = model.predict_proba(X_valid)[:, 1]
    metrics_raw = evaluate_binary(y_valid, p_valid)

    # Calibrate using Platt on log-odds scores (or just probs)
    scores = model.decision_function(X_valid)
    calibrator = PlattCalibrator().fit(scores, y_valid)
    p_valid_cal = calibrator.predict_proba(scores)
    metrics_cal = evaluate_binary(y_valid, p_valid_cal)

    meta = {
        "model_type": "logistic_regression",
        "train_rows": int(len(train_df)),
        "valid_rows": int(len(valid_df)),
        "metrics_raw": metrics_raw,
        "metrics_calibrated": metrics_cal,
    }

    save_artifacts(
        out_dir=args.out,
        model=model,
        preprocessor=fb.preprocessor,
        calibrator=calibrator,
        schema_hash=schema_hash(schema),
        metadata=meta
    )

    print(f"Saved artifacts to: {args.out}")
    print(f"\nTraining completed:")
    print(f"  Training samples: {len(train_df)}")
    print(f"  Validation samples: {len(valid_df)}")
    print(f"\nRaw metrics (ROC-AUC): {metrics_raw.get('roc_auc', 'N/A'):.4f}")
    print(f"Calibrated metrics (ROC-AUC): {metrics_cal.get('roc_auc', 'N/A'):.4f}")
    print(f"\nFull metrics saved in training_metadata.json")

if __name__ == "__main__":
    main()
