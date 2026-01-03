# batch prediction
# src/predict.py
"""
Batch prediction for no-show probability.

Loads trained model artifacts and generates predictions for input CSV.
Outputs predictions with confidence intervals and top feature drivers.
"""
import argparse
import sys
import pandas as pd
import numpy as np
from typing import List

from features import load_schema, schema_hash, validate_df, transform
from artifacts import load_artifacts


def top_drivers_logistic(model, feature_names: List[str], x_row: np.ndarray, top_k: int = 2) -> List[str]:
    """
    Extract top contributing features for a single prediction (logistic regression only).
    
    Args:
        model: Trained logistic regression model (must have coef_ attribute)
        feature_names: List of feature names
        x_row: Single row of preprocessed features (1D array)
        top_k: Number of top drivers to return
    
    Returns:
        List of top k feature names contributing most to prediction
    """
    if not hasattr(model, 'coef_'):
        # Not a logistic regression model - return empty list
        return []
    
    contrib = x_row * model.coef_.ravel()
    idx = np.argsort(-np.abs(contrib))[:top_k]
    return [feature_names[i] for i in idx]

def predict_batch(
    df: pd.DataFrame,
    model,
    preprocessor,
    calibrator,
    schema_path: str = None,
    return_warnings: bool = False
) -> tuple:
    """
    Predict no-show probabilities for a batch of appointments.
    
    Args:
        df: DataFrame with appointment features
        model: Trained model
        preprocessor: Fitted preprocessor
        calibrator: Calibrator (optional)
        schema_path: Path to schema.json for validation
        return_warnings: If True, return warnings list as third element
    
    Returns:
        Tuple of (probabilities, schema_hash, warnings) if return_warnings=True,
        else (probabilities, schema_hash)
    """
    warnings = []
    
    # Validate if schema provided
    schema_hash_check = None
    if schema_path:
        try:
            schema = load_schema(schema_path)
            validate_df(df, schema)
            schema_hash_check = schema_hash(schema)
        except Exception as e:
            if return_warnings:
                warnings.append(f"Schema validation warning: {str(e)}")
            else:
                raise
    
    # Transform features
    X = transform(preprocessor, df)
    
    # Generate predictions
    p = model.predict_proba(X)[:, 1]
    
    # Apply calibration if available
    if calibrator is not None:
        # Try decision_function if available (for Platt scaling with log-odds)
        # Otherwise use raw probabilities
        if hasattr(model, 'decision_function'):
            try:
                raw_scores = model.decision_function(X)
                p = calibrator.predict_proba(raw_scores)
            except Exception:
                # Fallback to using probabilities directly
                p = calibrator.predict_proba(p)
        else:
            # Model doesn't have decision_function, use probabilities
            p = calibrator.predict_proba(p)
    
    if return_warnings:
        return p, schema_hash_check, warnings
    return p, schema_hash_check


def main():
    ap = argparse.ArgumentParser(description="Generate predictions for appointment no-show probability")
    ap.add_argument("--csv", required=True, help="Path to input CSV file")
    ap.add_argument("--schema", required=True, help="Path to schema.json file")
    ap.add_argument("--artifacts", required=True, help="Path to model artifacts directory")
    ap.add_argument("--out", default="predictions.csv", help="Output CSV file path (default: predictions.csv)")
    ap.add_argument("--top_k", type=int, default=2, help="Number of top feature drivers to return (default: 2)")
    args = ap.parse_args()

    # Load and validate input data
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

    # Load model artifacts
    try:
        model, pre, calibrator, saved_hash, meta = load_artifacts(args.artifacts)
    except Exception as e:
        print(f"Error loading artifacts: {e}", file=sys.stderr)
        sys.exit(1)

    # Validate schema hash matches training schema
    current_hash = schema_hash(schema)
    if saved_hash != current_hash:
        raise ValueError(
            f"Schema hash mismatch: input schema differs from training schema.\n"
            f"  Training hash: {saved_hash}\n"
            f"  Current hash: {current_hash}\n"
            f"This indicates the input data structure doesn't match what the model was trained on."
        )

    # Use predict_batch function
    p, _ = predict_batch(df, model, pre, calibrator, schema_path=args.schema)

    # Lightweight confidence interval heuristic for demo
    # (Simple: +/- 0.08 clipped to [0, 1])
    # TODO: Replace with bootstrap or quantile regression for production
    lo = np.clip(p - 0.08, 0, 1)
    hi = np.clip(p + 0.08, 0, 1)

    # Extract top feature drivers (works for logistic regression, empty for other models)
    feature_names = list(pre.get_feature_names_out())
    drivers = []
    for i in range(X.shape[0]):
        drivers.append(top_drivers_logistic(model, feature_names, X[i], top_k=args.top_k))

    # Build output DataFrame
    out = df.copy()
    out["no_show_probability"] = p
    out["confidence_low"] = lo
    out["confidence_high"] = hi
    if any(drivers):  # Only add if we have drivers
        out["top_drivers"] = [",".join(d) for d in drivers]

    # Save predictions
    try:
        out.to_csv(args.out, index=False)
        print(f"Predictions written to: {args.out}")
        print(f"  Total predictions: {len(out)}")
        print(f"  Mean no-show probability: {p.mean():.4f}")
    except Exception as e:
        print(f"Error writing predictions: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
