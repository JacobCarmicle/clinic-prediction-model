# save/load model + schema hash + metadata
# src/artifacts.py
"""
Model artifact management: save and load trained models, preprocessors, calibrators,
schema hashes, and training metadata.

Artifacts are saved as:
- model.joblib: Trained model
- preprocessor.joblib: Feature preprocessor (ColumnTransformer)
- calibrator.joblib: Optional probability calibrator
- schema_hash.txt: SHA256 hash of feature schema (for validation)
- training_metadata.json: Training metrics, timestamps, hyperparameters
"""
import json
import os
import joblib
from datetime import datetime
from typing import Any, Dict, Optional, Tuple


def ensure_dir(path: str) -> None:
    """
    Ensure directory exists, creating it if necessary.
    
    Args:
        path: Directory path to ensure exists
    """
    os.makedirs(path, exist_ok=True)


def save_artifacts(
    out_dir: str,
    model: Any,
    preprocessor: Any,
    calibrator: Optional[Any],
    schema_hash: str,
    metadata: Dict[str, Any]
) -> None:
    """
    Save model artifacts to directory.
    
    Saves:
    - model.joblib: Trained model
    - preprocessor.joblib: Feature preprocessor
    - calibrator.joblib: Optional calibrator (if provided)
    - schema_hash.txt: Schema hash for validation
    - training_metadata.json: Training metadata with timestamp
    
    Args:
        out_dir: Output directory (created if doesn't exist)
        model: Trained model object (e.g., LogisticRegression, XGBoost)
        preprocessor: Fitted ColumnTransformer
        calibrator: Optional probability calibrator (PlattCalibrator or IsotonicCalibrator)
        schema_hash: SHA256 hash of feature schema
        metadata: Dictionary with training metrics, hyperparameters, etc.
    
    Raises:
        ValueError: If schema_hash is empty
        OSError: If directory cannot be created or files cannot be written
    """
    if not schema_hash or not schema_hash.strip():
        raise ValueError("schema_hash cannot be empty")
    
    ensure_dir(out_dir)
    
    # Save model and preprocessor (required)
    joblib.dump(model, os.path.join(out_dir, "model.joblib"))
    joblib.dump(preprocessor, os.path.join(out_dir, "preprocessor.joblib"))
    
    # Save calibrator if provided (optional)
    if calibrator is not None:
        joblib.dump(calibrator, os.path.join(out_dir, "calibrator.joblib"))

    # Save schema hash
    schema_hash_path = os.path.join(out_dir, "schema_hash.txt")
    with open(schema_hash_path, "w") as f:
        f.write(schema_hash.strip())

    # Save metadata with timestamp (create copy to avoid mutating input)
    metadata_copy = dict(metadata)
    metadata_copy["saved_at"] = datetime.utcnow().isoformat() + "Z"
    
    metadata_path = os.path.join(out_dir, "training_metadata.json")
    with open(metadata_path, "w") as f:
        json.dump(metadata_copy, f, indent=2)


def load_artifacts(in_dir: str) -> Tuple[Any, Any, Optional[Any], str, Dict[str, Any]]:
    """
    Load model artifacts from directory.
    
    Loads:
    - model.joblib: Trained model
    - preprocessor.joblib: Feature preprocessor
    - calibrator.joblib: Optional calibrator (if exists)
    - schema_hash.txt: Schema hash for validation
    - training_metadata.json: Training metadata (empty dict if missing)
    
    Args:
        in_dir: Input directory containing artifacts
    
    Returns:
        Tuple of (model, preprocessor, calibrator, schema_hash, metadata)
        - calibrator will be None if calibrator.joblib doesn't exist
        - metadata will be {} if training_metadata.json doesn't exist
    
    Raises:
        FileNotFoundError: If required files (model.joblib, preprocessor.joblib, schema_hash.txt) are missing
        ValueError: If schema_hash.txt is empty
    """
    if not os.path.isdir(in_dir):
        raise FileNotFoundError(f"Artifact directory does not exist: {in_dir}")
    
    # Load required files
    model_path = os.path.join(in_dir, "model.joblib")
    preprocessor_path = os.path.join(in_dir, "preprocessor.joblib")
    schema_hash_path = os.path.join(in_dir, "schema_hash.txt")
    
    if not os.path.exists(model_path):
        raise FileNotFoundError(f"Required artifact missing: {model_path}")
    if not os.path.exists(preprocessor_path):
        raise FileNotFoundError(f"Required artifact missing: {preprocessor_path}")
    if not os.path.exists(schema_hash_path):
        raise FileNotFoundError(f"Required artifact missing: {schema_hash_path}")
    
    model = joblib.load(model_path)
    preprocessor = joblib.load(preprocessor_path)
    
    # Load optional calibrator
    calibrator_path = os.path.join(in_dir, "calibrator.joblib")
    calibrator = joblib.load(calibrator_path) if os.path.exists(calibrator_path) else None

    # Load schema hash
    with open(schema_hash_path, "r") as f:
        schema_hash = f.read().strip()

    if not schema_hash:
        raise ValueError(f"schema_hash.txt is empty in {in_dir}")
    
    # Load optional metadata
    metadata_path = os.path.join(in_dir, "training_metadata.json")
    if os.path.exists(metadata_path):
        with open(metadata_path, "r") as f:
            metadata = json.load(f)
    else:
        metadata = {}
    
    return model, preprocessor, calibrator, schema_hash, metadata
