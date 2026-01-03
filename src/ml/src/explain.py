# coefficients / feature importance / optional SHAP
# src/explain.py
"""
Explainability module for model predictions.

Provides global feature importance and local explanations (per-prediction drivers).
Supports logistic regression (coefficients) and GBDT models (feature importances).
"""
from __future__ import annotations
from typing import List, Dict, Any, Optional
import numpy as np


def global_importance_logistic(model: Any, feature_names: List[str], top_k: int = 20) -> List[Dict[str, Any]]:
    """
    Extract global feature importance for logistic regression model.
    
    Returns top features by absolute coefficient magnitude, indicating which features
    have the strongest impact on predictions overall.
    
    Args:
        model: Trained logistic regression model (must have coef_ attribute)
        feature_names: List of feature names (must match model dimensions)
        top_k: Number of top features to return
    
    Returns:
        List of dicts with keys: "feature", "coefficient", "abs"
        Sorted by absolute coefficient value (descending)
    
    Raises:
        AttributeError: If model doesn't have coef_ attribute
        ValueError: If feature_names length doesn't match model dimensions
    """
    if not hasattr(model, "coef_"):
        raise AttributeError("Model must have coef_ attribute (logistic regression)")
    
    coefs = model.coef_.ravel()
    
    if len(feature_names) != len(coefs):
        raise ValueError(
            f"Feature names length ({len(feature_names)}) doesn't match "
            f"model coefficients length ({len(coefs)})"
        )
    
    idx = np.argsort(-np.abs(coefs))[:top_k]
    return [
        {
            "feature": feature_names[i],
            "coefficient": float(coefs[i]),
            "abs": float(abs(coefs[i]))
        }
        for i in idx
    ]


def top_drivers_logistic(model: Any, feature_names: List[str], x_row: np.ndarray, top_k: int = 2) -> List[str]:
    """
    Local explanation for one prediction: largest absolute contributions (x_i * coef_i).
    
    For logistic regression, contribution = feature_value * coefficient.
    This identifies which features contributed most to a specific prediction.
    
    Args:
        model: Trained logistic regression model (must have coef_ attribute)
        feature_names: List of feature names (must match model dimensions)
        x_row: Single row of transformed features (1D array)
        top_k: Number of top drivers to return
    
    Returns:
        List of top k feature names contributing most to this prediction
    
    Raises:
        AttributeError: If model doesn't have coef_ attribute
        ValueError: If dimensions don't match
    """
    if not hasattr(model, "coef_"):
        raise AttributeError("Model must have coef_ attribute (logistic regression)")
    
    coefs = model.coef_.ravel()
    x_row = np.asarray(x_row).ravel()
    
    if len(x_row) != len(coefs):
        raise ValueError(
            f"Feature vector length ({len(x_row)}) doesn't match "
            f"model coefficients length ({len(coefs)})"
        )
    
    if len(feature_names) != len(coefs):
        raise ValueError(
            f"Feature names length ({len(feature_names)}) doesn't match "
            f"model coefficients length ({len(coefs)})"
        )
    
    contrib = x_row * coefs
    idx = np.argsort(-np.abs(contrib))[:top_k]
    return [feature_names[i] for i in idx]


def global_importance_gbdt(model: Any, feature_names: List[str], top_k: int = 20) -> List[Dict[str, Any]]:
    """
    Extract global feature importance for GBDT models (XGBoost, LightGBM, sklearn).
    
    Works with any model that exposes `feature_importances_` attribute.
    Interpretation depends on library (often split count or gain proxy).
    
    Args:
        model: Trained GBDT model (must have feature_importances_ attribute)
        feature_names: List of feature names (must match model dimensions)
        top_k: Number of top features to return
    
    Returns:
        List of dicts with keys: "feature", "importance"
        Sorted by importance value (descending)
    
    Raises:
        AttributeError: If model doesn't have feature_importances_ attribute
        ValueError: If feature_names length doesn't match model dimensions
    """
    if not hasattr(model, "feature_importances_"):
        raise AttributeError("Model must have feature_importances_ attribute (GBDT models)")
    
    imp = np.asarray(model.feature_importances_, dtype=float)
    
    if len(feature_names) != len(imp):
        raise ValueError(
            f"Feature names length ({len(feature_names)}) doesn't match "
            f"model feature importances length ({len(imp)})"
        )
    
    idx = np.argsort(-imp)[:top_k]
    return [
        {
            "feature": feature_names[i],
            "importance": float(imp[i])
        }
        for i in idx
    ]


def top_drivers_gbdt_placeholder(
    feature_names: List[str],
    global_importance: List[Dict[str, Any]],
    top_k: int = 2
) -> List[str]:
    """
    Placeholder for GBDT local explanations without SHAP.
    
    Without SHAP, the best lightweight approximation is to use global top features.
    For real per-row explanations, add SHAP integration later.
    
    Args:
        feature_names: List of all feature names (unused, kept for API compatibility)
        global_importance: List of global importance dicts (sorted by importance)
        top_k: Number of top drivers to return
    
    Returns:
        List of top k feature names from global importance
    """
    return [d["feature"] for d in global_importance[:top_k]]


def build_explanations(
    model_type: str,
    model: Any,
    feature_names: List[str],
    X: Optional[np.ndarray] = None,
    per_row_top_k: int = 2,
    global_top_k: int = 20
) -> Dict[str, Any]:
    """
    Build complete explanation package for a model and dataset.
    
    Returns a dict suitable for API/UI consumption with:
    - Global feature importance (always)
    - Per-row top drivers (if X provided)
    
    Args:
        model_type: Type of model - "logistic_regression" or "gbdt"
        model: Trained model instance
        feature_names: List of feature names
        X: Optional array of transformed features (for per-row explanations)
        per_row_top_k: Number of top drivers per prediction
        global_top_k: Number of top features for global importance
    
    Returns:
        Dictionary with:
        - "global_importance": List of feature importance dicts
        - "top_drivers_per_row": List of lists of feature names (if X provided)
    
    Raises:
        ValueError: If model_type is unknown or dimensions don't match
    """
    if model_type == "logistic_regression":
        g = global_importance_logistic(model, feature_names, top_k=global_top_k)
        out = {"global_importance": g}
        
        if X is not None:
            X = np.asarray(X)
            if len(X.shape) == 1:
                X = X.reshape(1, -1)
            
            drivers = [
                top_drivers_logistic(model, feature_names, X[i], top_k=per_row_top_k)
                for i in range(X.shape[0])
            ]
            out["top_drivers_per_row"] = drivers
        
        return out

    if model_type == "gbdt":
        g = global_importance_gbdt(model, feature_names, top_k=global_top_k)
        out = {"global_importance": g}
        
        if X is not None:
            X = np.asarray(X)
            if len(X.shape) == 1:
                X = X.reshape(1, -1)
            
            # Placeholder until SHAP is added
            drivers = [
                top_drivers_gbdt_placeholder(feature_names, g, top_k=per_row_top_k)
                for _ in range(X.shape[0])
            ]
            out["top_drivers_per_row"] = drivers
        
        return out

    raise ValueError(
        f"Unknown model_type: {model_type}. "
        f"Expected 'logistic_regression' or 'gbdt'"
    )
