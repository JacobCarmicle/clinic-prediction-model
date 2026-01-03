# Quick Start Guide - Using ClinicFlow

This guide will get you up and running with ClinicFlow in minutes.

## Prerequisites

- Python 3.8+ with ML dependencies installed
- .NET 8 SDK
- Trained model artifacts in `src/ml/artifacts/gbdt/`

## Step 1: Start the ML Service

Open Terminal 1:

```bash
cd src/ml/src
python3 api_service.py
```

You should see:
```
✅ Model loaded: gbdt
   Schema hash: <hash>...
   Training samples: <number>
INFO:     Started server process
INFO:     Uvicorn running on http://0.0.0.0:8000
```

The ML service is now running on `http://localhost:8000`

## Step 2: Start the .NET API

Open Terminal 2:

```bash
cd src/api_dotnet/ClinicFlow.api
dotnet run
```

You should see:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5127
```

The API is now running on `http://localhost:5127`

## Step 3: Open the Web Interface

Open your browser to: **http://localhost:5127**

You'll see the ClinicFlow interface with:
- Clinic profile configuration
- Sample data loaded automatically
- "Analyze Schedule" button

## Step 4: Analyze a Schedule

1. The sample data is already loaded
2. Click **"🔍 Analyze Schedule"**
3. Wait a few seconds for the analysis
4. View the results:
   - Expected kept appointments
   - Expected revenue
   - Capacity metrics
   - Overflow risk
   - Per-slot no-show predictions
   - Segment analysis

## Using Your Own Data

### Option 1: JSON Format

Paste JSON in the text area:

```json
{
  "clinicProfile": {
    "clinicType": "PrimaryCare",
    "providersPerBlock": 3,
    "avgServiceMinutes": 15,
    "timeBlockMinutes": 240
  },
  "appointments": [
    {
      "appointmentId": 1,
      "appointmentType": "FollowUp",
      "dayOfWeek": "Tuesday",
      "timeBlock": "Morning",
      "leadTimeDays": 14,
      "isNewPatient": false,
      "historicalNoShowRate": 0.18,
      "unitRevenue": 120.0
    }
  ]
}
```

### Option 2: CSV Format

Upload a CSV file with these columns:
- `appointment_type`
- `day_of_week`
- `time_block`
- `lead_time_days`
- `is_new_patient` (true/false)
- `historical_no_show_rate`
- `unit_revenue`

## Testing via API Directly

### Health Check

```bash
curl http://localhost:5127/health
```

### Analyze Schedule

```bash
curl -X POST http://localhost:5127/schedule/analyze \
  -H "Content-Type: application/json" \
  -d @test_request.json
```

## Troubleshooting

### ML Service won't start
- **Error**: `FileNotFoundError: Artifact directory does not exist`
- **Fix**: Make sure you've trained a model first. Run:
  ```bash
  cd src/ml/src
  python3 train_gbdt.py --csv "../synthetic data/synthetic_appointments.csv" \
    --schema ../config/schema.json --out ../artifacts/gbdt --train_frac 0.8
  ```

### API can't reach ML service
- **Error**: "ML inference service unavailable"
- **Fix**: 
  1. Make sure ML service is running on port 8000
  2. Check `appsettings.json` has correct `MLService:BaseUrl`
  3. Try accessing `http://localhost:8000/health` directly

### UI not loading
- **Error**: 404 or blank page
- **Fix**: 
  1. Make sure `wwwroot/index.html` exists
  2. Check that `UseStaticFiles()` is in `Program.cs`
  3. Try accessing `http://localhost:5127/index.html` directly

## Next Steps

- Review the full [Building Interface Guide](BUILDING_INTERFACE_GUIDE.md)
- Check the [ML Operations Guide](ML_OPERATIONS_GUIDE.md) for training models
- See [API Integration Guide](API_INTEGRATION_GUIDE.md) for API details

---

**Ready to optimize your clinic schedule!** 🚀

