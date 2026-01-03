# xgboost/lightgbm pipeline
# src/train_gbdt.py
"""
Train Gradient Boosted Decision Tree (GBDT) model for no-show prediction.

Uses LightGBM for fast, memory-efficient training with early stopping.
Calibrates probabilities using Isotonic Regression.

Example:
    python train_gbdt.py --csv data/train.csv --schema config/schema.json --out artifacts_gbdt
"""
import argparse
import sys
import pandas as pd

from features import load_schema, schema_hash, validate_df, fit_transform, transform
from split import temporal_split
from evaluate import evaluate_binary
from calibrate import IsotonicCalibrator  # often works well with GBDT probs
from artifacts import save_artifacts


def main():
    ap = argparse.ArgumentParser(description="Train GBDT model (LightGBM) for no-show prediction")
    ap.add_argument("--csv", required=True, help="Path to training CSV file")
    ap.add_argument("--schema", required=True, help="Path to schema.json file")
    ap.add_argument("--out", default="artifacts_gbdt", help="Output directory for artifacts (default: artifacts_gbdt)")
    ap.add_argument("--train_frac", type=float, default=0.8, help="Fraction of data for training (default: 0.8)")
    ap.add_argument("--time_col", default=None, help="Optional time column for temporal split")
    ap.add_argument("--num_leaves", type=int, default=31, help="Number of leaves in tree (default: 31)")
    ap.add_argument("--learning_rate", type=float, default=0.05, help="Learning rate (default: 0.05)")
    ap.add_argument("--n_estimators", type=int, default=2000, help="Maximum number of boosting rounds (default: 2000)")
    ap.add_argument("--early_stopping_rounds", type=int, default=50, help="Early stopping patience (default: 50)")
    ap.add_argument("--scale_numeric", action="store_false", default=True, help="Disable scaling of numeric features (scaling enabled by default)")
    args = ap.parse_args()

    # Import LightGBM with helpful error message
    try:
        from lightgbm import LGBMClassifier, early_stopping
    except ImportError as e:
        print(
            "Error: LightGBM not installed.\n"
            "Install with: pip install lightgbm\n"
            f"Original error: {e}",
            file=sys.stderr
        )
        sys.exit(1)

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
    
    # Get feature names for LightGBM (to avoid warnings)
    feature_names = fb.feature_names

    # Configure LightGBM model
    model = LGBMClassifier(
        n_estimators=args.n_estimators,
        learning_rate=args.learning_rate,
        num_leaves=args.num_leaves,
        objective="binary",
        subsample=0.9,
        colsample_bytree=0.9,
        reg_lambda=1.0,
        reg_alpha=0.1,
        random_state=42,
        n_jobs=-1,
        verbosity=-1,  # Suppress LightGBM output
    )

    # Train with early stopping
    # Convert to DataFrame with feature names to avoid warnings and improve interpretability
    X_train_df = pd.DataFrame(X_train, columns=feature_names)
    X_valid_df = pd.DataFrame(X_valid, columns=feature_names)
    
    model.fit(
        X_train_df, y_train,
        eval_set=[(X_valid_df, y_valid)],
        eval_metric="binary_logloss",
        callbacks=[
            early_stopping(stopping_rounds=args.early_stopping_rounds, verbose=False)
        ],
    )
    
    # Update X_valid for evaluation (use DataFrame)
    X_valid = X_valid_df

    # Get best iteration info (if early stopping triggered)
    best_iteration = getattr(model, 'best_iteration_', args.n_estimators)

    # Evaluate on validation set (X_valid is already DataFrame)
    p_valid = model.predict_proba(X_valid)[:, 1]
    metrics_raw = evaluate_binary(y_valid, p_valid)

    # Calibrate probabilities using Isotonic Regression
    calibrator = IsotonicCalibrator().fit(p_valid, y_valid)
    p_valid_cal = calibrator.predict_proba(p_valid)
    metrics_cal = evaluate_binary(y_valid, p_valid_cal)

    # Prepare metadata
    meta = {
        "model_type": "gbdt",
        "library": "lightgbm",
        "params": {
            "n_estimators": args.n_estimators,
            "learning_rate": args.learning_rate,
            "num_leaves": args.num_leaves,
            "early_stopping_rounds": args.early_stopping_rounds,
            "best_iteration": int(best_iteration),
            "early_stopped": best_iteration < args.n_estimators,
        },
        "train_rows": int(len(train_df)),
        "valid_rows": int(len(valid_df)),
        "metrics_raw": metrics_raw,
        "metrics_calibrated": metrics_cal,
    }

    # Save artifacts
    save_artifacts(
        out_dir=args.out,
        model=model,
        preprocessor=fb.preprocessor,
        calibrator=calibrator,
        schema_hash=schema_hash(schema),
        metadata=meta
    )

    # Print summary
    print(f"Saved artifacts to: {args.out}")
    print(f"\nTraining completed:")
    print(f"  Training samples: {len(train_df)}")
    print(f"  Validation samples: {len(valid_df)}")
    print(f"  Best iteration: {best_iteration}/{args.n_estimators}")
    print(f"  Early stopped: {'Yes' if best_iteration < args.n_estimators else 'No'}")
    print(f"\nRaw metrics (ROC-AUC): {metrics_raw.get('roc_auc', 'N/A'):.4f}")
    print(f"Calibrated metrics (ROC-AUC): {metrics_cal.get('roc_auc', 'N/A'):.4f}")
    print(f"\nFull metrics saved in training_metadata.json")

if __name__ == "__main__":
    main()
