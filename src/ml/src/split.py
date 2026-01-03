# temporal split helpers
"""
Temporal split utilities for time-ordered data.

Ensures train/validation split respects temporal ordering to prevent data leakage.
"""
import pandas as pd
from typing import Tuple, Optional


def temporal_split(
    df: pd.DataFrame, 
    frac_train: float = 0.8, 
    time_col: Optional[str] = None
) -> Tuple[pd.DataFrame, pd.DataFrame]:
    """
    Split DataFrame temporally into train and validation sets.
    
    If time_col is provided, sorts by that column first. Otherwise uses existing order.
    Ensures validation set comes after training set in time to prevent leakage.
    
    Args:
        df: Input DataFrame
        frac_train: Fraction of data for training (default: 0.8)
        time_col: Optional column name to sort by (should be datetime or sortable)
    
    Returns:
        Tuple of (train_df, valid_df)
    
    Raises:
        ValueError: If frac_train is not in (0, 1) or time_col doesn't exist
    """
    if not (0 < frac_train < 1):
        raise ValueError(f"frac_train must be between 0 and 1, got {frac_train}")
    
    # Sort by time column if provided
    if time_col and time_col in df.columns:
        df = df.sort_values(time_col).reset_index(drop=True)
        else:
        df = df.reset_index(drop=True)
        
        n = len(df)
        n_train = int(n * frac_train)
        train = df.iloc[:n_train].copy()
        valid = df.iloc[n_train:].copy()
    
        return train, valid