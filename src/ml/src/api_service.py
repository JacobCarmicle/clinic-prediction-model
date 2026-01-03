"""
FastAPI service for ML inference.

Matches the .NET API contract for integration.
"""
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import List, Optional
import os
import sys
import joblib
import numpy as np

# Add parent directory to path for imports
sys.path.insert(0, os.path.dirname(__file__))

from service_contract import (
    InferenceRequestDto, InferenceResponseDto, InferencePerSlotDto,
    from_dict_inference_request, to_dict
)
from features import transform, load_schema, schema_hash
from explain import build_explanations
from artifacts import load_artifacts

app = FastAPI(title="ClinicFlow ML Inference Service", version="1.0.0")

# Add CORS middleware for web UI
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # In production, restrict to specific origins
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Global model state
model = None
preprocessor = None
calibrator = None
schema = None
saved_hash = None
feature_names = None
model_meta = None

@app.on_event("startup")
async def load_model():
    """Load model artifacts on startup."""
    global model, preprocessor, calibrator, schema, saved_hash, feature_names, model_meta
    
    try:
        # Use environment variable or default path relative to this file
        # api_service.py is in src/ml/src/, so artifacts are at src/ml/artifacts/gbdt
        default_model_path = os.path.join(os.path.dirname(__file__), "..", "artifacts", "gbdt")
        default_schema_path = os.path.join(os.path.dirname(__file__), "..", "config", "schema.json")
        
        model_path = os.getenv("MODEL_PATH", default_model_path)
        schema_path = os.getenv("SCHEMA_PATH", default_schema_path)
        
        # Resolve absolute paths
        if not os.path.isabs(model_path):
            model_path = os.path.abspath(model_path)
        if not os.path.isabs(schema_path):
            schema_path = os.path.abspath(schema_path)
        
        model, preprocessor, calibrator, saved_hash, model_meta = load_artifacts(model_path)
        schema = load_schema(schema_path)
        feature_names = list(preprocessor.get_feature_names_out())
        
        model_type = model_meta.get('model_type', 'unknown')
        print(f"✅ Model loaded: {model_type}")
        print(f"   Schema hash: {saved_hash[:16]}...")
        print(f"   Training samples: {model_meta.get('train_rows', 'unknown')}")
    except Exception as e:
        print(f"❌ Failed to load model: {e}")
        import traceback
        traceback.print_exc()
        raise

@app.get("/health")
async def health():
    """Health check endpoint."""
    return {
        "status": "healthy",
        "model_loaded": model is not None,
        "model_type": model_meta.get('model_type', None) if model_meta else None,
        "schema_hash": saved_hash[:16] + "..." if saved_hash else None
    }

@app.post("/predict")
async def predict(request: dict):
    """
    Predict no-show probabilities for appointment slots.
    
    Matches .NET API ScheduleRequestDto structure.
    """
    try:
        # Parse request
        req_dto = from_dict_inference_request(request)
        
        # Convert appointments to DataFrame for processing
        import pandas as pd
        
        df_data = []
        for apt in req_dto.appointments:
            df_data.append({
                "appointment_type": apt.appointment_type,
                "day_of_week": apt.day_of_week,
                "time_block": apt.time_block,
                "lead_time_days": apt.lead_time_days,
                "is_new_patient": apt.is_new_patient,
                "historical_no_show_rate": apt.historical_no_show_rate,
                "unit_revenue": apt.unit_revenue,
            })
        
        df = pd.DataFrame(df_data)
        
        # Validate schema
        current_hash = schema_hash(schema)
        if saved_hash != current_hash:
            raise HTTPException(
                status_code=400,
                detail="Schema hash mismatch"
            )
        
        # Transform features
        X = transform(preprocessor, df)
        
        # Convert to DataFrame with feature names for LightGBM compatibility
        if hasattr(model, 'feature_name_'):
            X_df = pd.DataFrame(X, columns=feature_names)
        else:
            X_df = pd.DataFrame(X, columns=feature_names)
        
        # Generate predictions
        probs = model.predict_proba(X_df)[:, 1]
        
        # Apply calibration
        if calibrator is not None:
            try:
                # For Platt scaling, use decision_function if available (logistic regression)
                # For Isotonic, use probabilities directly
                if hasattr(calibrator, 'predict_proba'):
                    # Check if calibrator expects scores (decision_function) or probabilities
                    # Platt scaling typically uses scores, Isotonic uses probabilities
                    if hasattr(model, 'decision_function'):
                        try:
                            scores = model.decision_function(X_df)
                            # Reshape if needed
                            if scores.ndim == 1:
                                scores = scores.reshape(-1, 1)
                            probs_cal = calibrator.predict_proba(scores)
                            # Extract positive class probabilities
                            if probs_cal.ndim > 1 and probs_cal.shape[1] > 1:
                                probs = probs_cal[:, 1]
                            else:
                                probs = probs_cal.flatten()
                        except:
                            # Fallback to probabilities
                            probs = calibrator.predict_proba(probs.reshape(-1, 1))
                            if probs.ndim > 1 and probs.shape[1] > 1:
                                probs = probs[:, 1]
                            else:
                                probs = probs.flatten()
                    else:
                        # Model doesn't have decision_function, use probabilities
                        probs_cal = calibrator.predict_proba(probs.reshape(-1, 1))
                        if probs_cal.ndim > 1 and probs_cal.shape[1] > 1:
                            probs = probs_cal[:, 1]
                        else:
                            probs = probs_cal.flatten()
            except Exception as e:
                # If calibration fails, use original probabilities
                import warnings
                warnings.warn(f"Calibration failed: {e}, using uncalibrated probabilities")
                pass
        
        # Confidence intervals (heuristic)
        lo = np.clip(probs - 0.08, 0, 1)
        hi = np.clip(probs + 0.08, 0, 1)
        
        # Build response
        predictions = []
        for i, prob in enumerate(probs):
            # Get top drivers if explain=True
            top_drivers = []
            if req_dto.explain:
                model_type = "logistic_regression" if hasattr(model, 'coef_') else "gbdt"
                
                if model_type == "logistic_regression":
                    from explain import top_drivers_logistic
                    # Get single row from transformed features
                    X_row = X[i] if X.ndim == 2 else X
                    top_drivers = top_drivers_logistic(model, feature_names, X_row, top_k=2)
                else:
                    # Use global importance as placeholder for GBDT
                    exp = build_explanations(model_type, model, feature_names, global_top_k=2)
                    top_drivers = [f["feature"] for f in exp["global_importance"][:2]]
            
            predictions.append(
                InferencePerSlotDto(
                    no_show_probability=float(prob),
                    confidence_interval=(float(lo[i]), float(hi[i])),
                    top_drivers=top_drivers
                )
            )
        
        response = InferenceResponseDto(
            predictions=predictions,
            schema_hash=saved_hash,
            warnings=[]
        )
        
        return to_dict(response)
    
    except HTTPException:
        raise
    except Exception as e:
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))

if __name__ == "__main__":
    import uvicorn
    port = int(os.getenv("PORT", "8000"))
    uvicorn.run(app, host="0.0.0.0", port=port)
