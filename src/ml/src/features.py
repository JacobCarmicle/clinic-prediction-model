# src/features.py
import json
import hashlib
from dataclasses import dataclass
from typing import List, Tuple, Dict, Any, Optional

import numpy as np
import pandas as pd
from sklearn.compose import ColumnTransformer
from sklearn.preprocessing import OneHotEncoder, StandardScaler
from sklearn.pipeline import Pipeline
from sklearn.impute import SimpleImputer


REQUIRED_COLS = [
    "appointment_type",
    "day_of_week",
    "time_block",
    "lead_time_days",
    "is_new_patient",
    "historical_no_show_rate",
    "unit_revenue",
]

TARGET_COL = "no_show"


def load_schema(schema_path: str) -> Dict[str, Any]:
    with open(schema_path, "r") as f:
        return json.load(f)


def schema_hash(schema: Dict[str, Any]) -> str:
    raw = json.dumps(schema, sort_keys=True).encode("utf-8")
    return hashlib.sha256(raw).hexdigest()


def validate_df(df: pd.DataFrame, schema: Dict[str, Any]) -> None:
    """Validate DataFrame against schema.json structure."""
    # Hard required columns
    missing = [c for c in REQUIRED_COLS if c not in df.columns]
    if missing:
        raise ValueError(f"Missing required columns: {missing}")

    # Build lookup from schema feature_columns
    feature_specs = {f["name"]: f for f in schema.get("feature_columns", [])}
    
    # Enum validation for categorical features
    for col_name, spec in feature_specs.items():
        if col_name not in df.columns:
            continue
            
        if spec.get("type") == "categorical" and "enum_values" in spec:
            allowed = set(spec["enum_values"])
            actual = set(df[col_name].dropna().unique())
            bad = actual - allowed
            if bad:
                raise ValueError(f"Invalid enum values in {col_name}: {sorted(bad)}. Allowed: {sorted(allowed)}")
        
        # Range validation for numeric features
        if spec.get("type") == "numeric":
            col_data = df[col_name].dropna()
            if "min" in spec and len(col_data) > 0 and (col_data < spec["min"]).any():
                raise ValueError(f"{col_name} has values < {spec['min']}")
            if "max" in spec and len(col_data) > 0 and (col_data > spec["max"]).any():
                raise ValueError(f"{col_name} has values > {spec['max']}")

    # Target validation if present
    if TARGET_COL in df.columns:
        target_spec = schema.get("target_column", {})
        allowed_values = set(target_spec.get("values", [0, 1]))
        actual_values = set(df[TARGET_COL].dropna().unique())
        bad_t = actual_values - allowed_values
        if bad_t:
            raise ValueError(f"Invalid target values in {TARGET_COL}: {sorted(bad_t)}. Allowed: {sorted(allowed_values)}")


@dataclass
class FeatureBundle:
    X: Any
    y: np.ndarray
    preprocessor: ColumnTransformer
    feature_names: List[str]


def build_preprocessor(scale_numeric: bool = True) -> ColumnTransformer:
    """
    Build feature preprocessor pipeline.
    
    Args:
        scale_numeric: If True, apply StandardScaler to numeric features (recommended for logistic regression)
    
    Returns:
        ColumnTransformer configured for the appointment features
    """
    cat_cols = ["appointment_type", "day_of_week", "time_block"]
    num_cols = ["lead_time_days", "historical_no_show_rate", "unit_revenue"]
    bool_cols = ["is_new_patient"]

    # Categorical: impute missing (shouldn't happen with our data) then one-hot encode
    categorical = Pipeline(steps=[
        ("imputer", SimpleImputer(strategy="most_frequent")),
        ("onehot", OneHotEncoder(handle_unknown="ignore", sparse_output=False, drop="if_binary")),
    ])

    # Numeric: impute then optionally scale
    numeric_steps = [("imputer", SimpleImputer(strategy="median"))]
    if scale_numeric:
        numeric_steps.append(("scaler", StandardScaler()))
    numeric = Pipeline(steps=numeric_steps)

    # Boolean: convert to int (0/1), no imputation needed since it's required
    # Simple passthrough with conversion - bool columns are already 0/1 in our data
    # But we ensure they're int type
    from sklearn.preprocessing import FunctionTransformer
    def ensure_int(X):
        """Ensure boolean-like column is int (0/1)."""
        if hasattr(X, 'astype'):
            return X.astype(int).values.reshape(-1, 1)
        return np.asarray(X, dtype=int).reshape(-1, 1)
    
    boolean = Pipeline(steps=[
        ("to_int", FunctionTransformer(ensure_int, validate=False, accept_sparse=False)),
    ])

    return ColumnTransformer(
        transformers=[
            ("cat", categorical, cat_cols),
            ("num", numeric, num_cols),
            ("bool", boolean, bool_cols),
        ],
        remainder="drop",
        verbose_feature_names_out=False
    )


def fit_transform(df: pd.DataFrame, scale_numeric: bool = True, schema_path: Optional[str] = None) -> FeatureBundle:
    """
    Fit preprocessor and transform features.
    
    Args:
        df: Input DataFrame with features and target
        scale_numeric: Whether to scale numeric features
        schema_path: Optional path to schema.json for validation
    
    Returns:
        FeatureBundle with X, y, preprocessor, and feature_names
    """
    # Optional validation
    if schema_path:
        schema = load_schema(schema_path)
        validate_df(df, schema)
    
    y = df[TARGET_COL].to_numpy(dtype=int)
    pre = build_preprocessor(scale_numeric=scale_numeric)
    X = pre.fit_transform(df)

    feature_names = list(pre.get_feature_names_out())
    return FeatureBundle(X=X, y=y, preprocessor=pre, feature_names=feature_names)


def transform(preprocessor: ColumnTransformer, df: pd.DataFrame) -> np.ndarray:
    """
    Transform features using a fitted preprocessor.
    
    Args:
        preprocessor: Fitted ColumnTransformer
        df: Input DataFrame with features
    
    Returns:
        Transformed feature matrix
    """
    return preprocessor.transform(df)
