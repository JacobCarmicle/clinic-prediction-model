# Testing Guide - ClinicFlow End-to-End

Complete guide for testing the ClinicFlow system.

---

## Quick Test Checklist

- [ ] ML service starts and loads model
- [ ] .NET API starts successfully
- [ ] Web UI loads in browser
- [ ] Health endpoints respond
- [ ] Schedule analysis works end-to-end
- [ ] CSV upload works
- [ ] JSON input works
- [ ] Error handling works

---

## 1. Test ML Service

### Start the Service

```bash
cd src/ml/src
python3 api_service.py
```

**Expected output:**
```
✅ Model loaded: gbdt
   Schema hash: <hash>...
   Training samples: <number>
INFO:     Started server process
INFO:     Uvicorn running on http://0.0.0.0:8000
```

### Test Health Endpoint

```bash
curl http://localhost:8000/health
```

**Expected response:**
```json
{
  "status": "healthy",
  "model_loaded": true,
  "model_type": "gbdt",
  "schema_hash": "<hash>..."
}
```

### Test Prediction Endpoint

```bash
curl -X POST http://localhost:8000/predict \
  -H "Content-Type: application/json" \
  -d '{
    "clinic_profile": {
      "clinic_type": "PrimaryCare",
      "providers_per_block": 3,
      "avg_service_minutes": 15,
      "time_block_minutes": 240
    },
    "appointments": [
      {
        "appointment_type": "FollowUp",
        "day_of_week": "Tuesday",
        "time_block": "Morning",
        "lead_time_days": 14,
        "is_new_patient": false,
        "historical_no_show_rate": 0.18,
        "unit_revenue": 120.0
      }
    ],
    "explain": true
  }'
```

**Expected response:**
```json
{
  "predictions": [
    {
      "no_show_probability": 0.15,
      "confidence_interval": [0.07, 0.23],
      "top_drivers": ["lead_time_days", "historical_no_show_rate"]
    }
  ],
  "schema_hash": "<hash>",
  "warnings": []
}
```

---

## 2. Test .NET API

### Start the API

```bash
cd src/api_dotnet/ClinicFlow.api
dotnet run
```

**Expected output:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5127
```

### Test Health Endpoint

```bash
curl http://localhost:5127/health
```

**Expected response:**
```json
{
  "status": "ok",
  "timestamp": "2024-..."
}
```

### Test Analyze Endpoint

```bash
curl -X POST http://localhost:5127/schedule/analyze \
  -H "Content-Type: application/json" \
  -d @test_request.json
```

**Expected response:**
```json
{
  "summary": {
    "expectedKeptAppointments": 2.4,
    "baselineRevenue": 330.0,
    "blockCapacityAppointments": 48.0,
    "expectedOverflowAppointments": 0.0,
    "isOverCapacity": false
  },
  "slotPredictions": [
    {
      "appointmentId": 1,
      "noShowProbability": 0.15
    }
  ],
  "segments": [...]
}
```

---

## 3. Test Web UI

### Open Browser

Navigate to: `http://localhost:5127`

### Test Features

1. **Sample Data Loading**
   - Click "📋 Load Sample Data"
   - Verify JSON populates in textarea

2. **CSV Template Download**
   - Click "📥 Download CSV Template"
   - Verify CSV downloads

3. **CSV Upload**
   - Click "Choose File"
   - Select `sample_schedule.csv`
   - Verify JSON populates from CSV

4. **Schedule Analysis**
   - Click "🔍 Analyze Schedule"
   - Wait for results
   - Verify metrics display
   - Verify predictions table
   - Verify segments table

5. **Error Handling**
   - Submit empty form → Should show error
   - Submit invalid JSON → Should show error
   - Stop ML service → Should fall back to historical rates

---

## 4. End-to-End Integration Test

### Automated Test Script

Create `test_e2e.sh`:

```bash
#!/bin/bash

echo "🧪 Testing ClinicFlow End-to-End"

# Test ML service
echo "1. Testing ML service..."
ML_HEALTH=$(curl -s http://localhost:8000/health | jq -r '.status')
if [ "$ML_HEALTH" = "healthy" ]; then
    echo "   ✅ ML service is healthy"
else
    echo "   ❌ ML service health check failed"
    exit 1
fi

# Test API health
echo "2. Testing API health..."
API_HEALTH=$(curl -s http://localhost:5127/health | jq -r '.status')
if [ "$API_HEALTH" = "ok" ]; then
    echo "   ✅ API is healthy"
else
    echo "   ❌ API health check failed"
    exit 1
fi

# Test analyze endpoint
echo "3. Testing analyze endpoint..."
RESPONSE=$(curl -s -X POST http://localhost:5127/schedule/analyze \
  -H "Content-Type: application/json" \
  -d '{
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
  }')

EXPECTED_KEPT=$(echo $RESPONSE | jq -r '.summary.expectedKeptAppointments')
if [ -n "$EXPECTED_KEPT" ] && [ "$EXPECTED_KEPT" != "null" ]; then
    echo "   ✅ Analyze endpoint works (expected kept: $EXPECTED_KEPT)"
else
    echo "   ❌ Analyze endpoint failed"
    echo "   Response: $RESPONSE"
    exit 1
fi

echo "✅ All tests passed!"
```

Run with:
```bash
chmod +x test_e2e.sh
./test_e2e.sh
```

---

## 5. Browser Testing

### Use API Tester Page

Navigate to: `http://localhost:5127/test-api.html`

This page provides interactive buttons to test:
- API health check
- ML service health check
- Analyze endpoint

### Use Browser DevTools

1. Open browser DevTools (F12)
2. Go to Network tab
3. Perform actions in UI
4. Verify API calls:
   - Status codes (200 OK)
   - Response times (< 2s)
   - Response formats (valid JSON)

---

## 6. Error Scenarios

### Test ML Service Down

1. Stop ML service (`Ctrl+C`)
2. Submit analysis request
3. **Expected:** API should fall back to historical rates
4. Check API logs for fallback message

### Test Invalid Request

1. Submit JSON with missing fields
2. **Expected:** Validation error with field details

### Test Empty Schedule

1. Submit request with empty appointments array
2. **Expected:** Validation error or empty results

---

## 7. Performance Testing

### Load Test (Simple)

```bash
# Run 10 concurrent requests
for i in {1..10}; do
    curl -X POST http://localhost:5127/schedule/analyze \
      -H "Content-Type: application/json" \
      -d @test_request.json &
done
wait
```

### Measure Response Time

```bash
time curl -X POST http://localhost:5127/schedule/analyze \
  -H "Content-Type: application/json" \
  -d @test_request.json
```

**Target:** < 2 seconds for typical request

---

## 8. Troubleshooting

### ML Service Won't Start

**Check:**
- Model artifacts exist: `ls src/ml/artifacts/gbdt/`
- Python dependencies installed: `pip list | grep fastapi`
- Port 8000 available: `lsof -i :8000`

**Solution:**
```bash
cd src/ml/src
python3 -m pip install -r ../../requirements.txt
python3 api_service.py
```

### API Can't Reach ML Service

**Check:**
- ML service running: `curl http://localhost:8000/health`
- Correct URL in `appsettings.json`: `MLService:BaseUrl`
- Network connectivity: `ping localhost`

**Solution:**
- Verify ML service is running on port 8000
- Check `appsettings.json` configuration

### UI Not Loading

**Check:**
- Static files enabled: `UseStaticFiles()` in `Program.cs`
- `wwwroot` directory exists
- Browser console for errors

**Solution:**
- Rebuild API: `dotnet build`
- Check file permissions
- Clear browser cache

---

## 9. Success Criteria

✅ **All systems operational when:**
- ML service responds to health checks
- API responds to health checks
- Web UI loads without errors
- Schedule analysis returns valid results
- Error messages are user-friendly
- Response times are acceptable (< 2s)

---

**Ready to test!** Start with the ML service, then API, then UI. 🚀

