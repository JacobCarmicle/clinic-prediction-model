# AUC/logloss/brier + calibration curve data
import numpy as np
from typing import Dict, List, Any
from sklearn.metrics import roc_auc_score, log_loss, brier_score_loss


def precision_at_top_k(y_true: np.ndarray, y_prob: np.ndarray, k_frac: float = 0.1) -> float:
    """
    Calculate precision at top k% of predictions (highest probability).
    
    Args:
        y_true: True binary labels
        y_prob: Predicted probabilities
        k_frac: Fraction of top predictions to consider (default 0.1 = top 10%)
    
    Returns:
        Precision (fraction of true positives) in top k%
    """
    k = max(1, int(len(y_true) * k_frac))
    idx = np.argsort(-y_prob)[:k]  # Sort descending, take top k
    return float(y_true[idx].mean())


def calibration_bins(y_true: np.ndarray, y_prob: np.ndarray, n_bins: int = 10) -> List[Dict[str, Any]]:
    """
    Compute calibration curve data by binning predictions.
    
    Args:
        y_true: True binary labels
        y_prob: Predicted probabilities
        n_bins: Number of bins for calibration (default 10)
    
    Returns:
        List of dicts with bin statistics: bin_low, bin_high, count, avg_pred, avg_true
    """
    bins = np.linspace(0, 1, n_bins + 1)
    ids = np.digitize(y_prob, bins) - 1
    # Clip to valid range (handle edge cases)
    ids = np.clip(ids, 0, n_bins - 1)
    
    out = []
    for b in range(n_bins):
        m = ids == b 
        if m.sum() == 0:
            continue
        out.append({
            "bin_low": float(bins[b]),
            "bin_high": float(bins[b + 1]),
            "count": int(m.sum()),
            "avg_pred": float(y_prob[m].mean()),
            "avg_true": float(y_true[m].mean()),
        })
        return out
    

def evaluate_binary(y_true: np.ndarray, y_prob: np.ndarray, n_calibration_bins: int = 10) -> Dict[str, Any]:
    """
    Evaluate binary classification model performance.
    
    Computes:
    - ROC-AUC: Ranking ability
    - Log Loss: Probability calibration quality
    - Brier Score: Forecast accuracy
    - Precision @ Top 10%: Operational focus on high-risk predictions
    - Calibration bins: Detailed calibration curve data
    
    Args:
        y_true: True binary labels (0 or 1)
        y_prob: Predicted probabilities (0-1)
        n_calibration_bins: Number of bins for calibration curve (default 10)
    
    Returns:
        Dictionary with all evaluation metrics
    """
    # Validate inputs
    if len(y_true) != len(y_prob):
        raise ValueError(f"y_true and y_prob must have same length: {len(y_true)} != {len(y_prob)}")
    
    if (y_prob < 0).any() or (y_prob > 1).any():
        raise ValueError("y_prob must contain values between 0 and 1")
    
    if set(np.unique(y_true)) - {0, 1}:
        raise ValueError("y_true must contain only 0 and 1")
    
    return {
            "roc_auc": float(roc_auc_score(y_true, y_prob)),
        "log_loss": float(log_loss(y_true, y_prob, labels=[0, 1])),
        "brier": float(brier_score_loss(y_true, y_prob)),
            "precision_top_10pct": precision_at_top_k(y_true, y_prob, 0.10),
        "calibration_bins": calibration_bins(y_true, y_prob, n_calibration_bins),
    }