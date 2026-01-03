# src/calibrate.py
"""
Probability calibration methods for binary classification.

Calibration improves probability estimates so they better reflect true likelihoods.
Useful for models (especially tree-based) that may be well-calibrated or overconfident.
"""
import numpy as np
from typing import Optional
from sklearn.isotonic import IsotonicRegression
from sklearn.linear_model import LogisticRegression


class PlattCalibrator:
    """
    Platt scaling (sigmoid calibration) using logistic regression.
    
    Maps uncalibrated probabilities/scores to calibrated probabilities via a sigmoid function.
    Good for models with good discrimination but poor calibration (e.g., SVMs, some tree models).
    
    Usage:
        calibrator = PlattCalibrator()
        calibrator.fit(y_uncalibrated, y_true)
        y_calibrated = calibrator.predict_proba(y_uncalibrated)
    """
    
    def __init__(self):
        self.model = LogisticRegression(solver="lbfgs", max_iter=1000)

    def fit(self, scores: np.ndarray, y: np.ndarray) -> "PlattCalibrator":
        """
        Fit calibration model on validation set predictions.
        
        Args:
            scores: Uncalibrated probabilities or scores (0-1 range for probabilities)
            y: True binary labels (0 or 1)
        
        Returns:
            self for chaining
        """
        scores = np.asarray(scores).reshape(-1, 1)
        y = np.asarray(y)
        
        if len(scores) != len(y):
            raise ValueError(f"scores and y must have same length: {len(scores)} != {len(y)}")
        
        if len(np.unique(y)) < 2:
            raise ValueError("Calibration requires both classes (0 and 1) to be present")
        
        self.model.fit(scores, y)
        return self

    def predict_proba(self, scores: np.ndarray) -> np.ndarray:
        """
        Predict calibrated probabilities.
        
        Args:
            scores: Uncalibrated probabilities or scores to calibrate
        
        Returns:
            Calibrated probabilities (0-1 range)
        """
        scores = np.asarray(scores).reshape(-1, 1)
        proba = self.model.predict_proba(scores)[:, 1]
        # Ensure probabilities are in valid range (should already be, but clip for safety)
        return np.clip(proba, 0.0, 1.0)


class IsotonicCalibrator:
    """
    Isotonic regression calibration (non-parametric piecewise-constant).
    
    More flexible than Platt scaling but requires more data. Works well when
    you have sufficient validation data. Can overfit on small datasets.
    
    Usage:
        calibrator = IsotonicCalibrator()
        calibrator.fit(y_uncalibrated, y_true)
        y_calibrated = calibrator.predict_proba(y_uncalibrated)
    """
    
    def __init__(self, out_of_bounds: str = "clip"):
        """
        Initialize isotonic calibrator.
        
        Args:
            out_of_bounds: How to handle predictions outside [0,1] range.
                          "clip" (default): clip to [0,1]
                          "nan": return NaN for out-of-bounds
        """
        self.iso = IsotonicRegression(out_of_bounds=out_of_bounds)

    def fit(self, probs: np.ndarray, y: np.ndarray) -> "IsotonicCalibrator":
        """
        Fit calibration model on validation set predictions.
        
        Args:
            probs: Uncalibrated probabilities (should be in 0-1 range)
            y: True binary labels (0 or 1)
        
        Returns:
            self for chaining
        """
        probs = np.asarray(probs).flatten()
        y = np.asarray(y)
        
        if len(probs) != len(y):
            raise ValueError(f"probs and y must have same length: {len(probs)} != {len(y)}")
        
        if len(np.unique(y)) < 2:
            raise ValueError("Calibration requires both classes (0 and 1) to be present")
        
        # Validate probability range
        if (probs < 0).any() or (probs > 1).any():
            raise ValueError("probs must be in [0, 1] range for isotonic calibration")
        
        self.iso.fit(probs, y)
        return self

    def predict_proba(self, probs: np.ndarray) -> np.ndarray:
        """
        Predict calibrated probabilities.
        
        Args:
            probs: Uncalibrated probabilities to calibrate (0-1 range)
        
        Returns:
            Calibrated probabilities (0-1 range)
        """
        probs = np.asarray(probs).flatten()
        calibrated = self.iso.predict(probs)
        # Ensure probabilities are in valid range
        return np.clip(calibrated, 0.0, 1.0)
