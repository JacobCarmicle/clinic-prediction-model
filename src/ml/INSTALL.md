# Installation Guide

## macOS Installation

### Step 1: Install System Dependencies

LightGBM requires OpenMP on macOS. Install via Homebrew:

```bash
# Install Homebrew if not already installed
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"

# Install OpenMP library
brew install libomp
```

### Step 2: Install Python Dependencies

**Important**: On macOS, use `python3 -m pip` instead of `pip`:

```bash
cd src/ml

# Install core dependencies
python3 -m pip install -r requirements.txt
```

### Alternative: If Homebrew is not available

If you can't install Homebrew, you can use XGBoost instead of LightGBM, or install LightGBM without OpenMP (slower but works):

```bash
python3 -m pip install --no-binary lightgbm lightgbm
```

## Verify Installation

```bash
# Test core dependencies
python3 -c "import numpy, pandas, sklearn, joblib; print('✅ Core dependencies OK')"

# Test LightGBM (after installing libomp)
python3 -c "import lightgbm; print('✅ LightGBM OK')"
```

## Quick Test

```bash
cd src/ml/src
python3 -c "from features import load_schema; print('✅ Imports working')"
```

## Troubleshooting

### Issue: `pip: command not found`
**Solution**: Use `python3 -m pip` instead of `pip`

### Issue: LightGBM import error (libomp missing)
**Solution**: 
```bash
brew install libomp
# Then reinstall lightgbm
python3 -m pip install --force-reinstall lightgbm
```

### Issue: Permission errors
**Solution**: Use `--user` flag:
```bash
python3 -m pip install --user -r requirements.txt
```

### Issue: Python version too old
**Solution**: Requires Python 3.8+. Check version:
```bash
python3 --version
```

