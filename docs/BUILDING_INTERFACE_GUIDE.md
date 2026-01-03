# Building the User Interface - Step-by-Step Guide

Complete guide to make ClinicFlow usable with a web interface.

---

## Current Status

✅ **Complete:**
- ML models trained and ready
- .NET API controllers and DTOs
- InferenceService implementation
- Validation and error handling

⚠️ **Needs Work:**
- Python ML service (api_service.py needs completion)
- API service registration (fix Program.cs)
- Simple web UI (doesn't exist yet)
- End-to-end testing

---

## Phase 1: Complete ML Service Integration

### Step 1.1: Create Python ML API Service

The service exists in concept but needs to be created. Let's create it:

**File: `src/ml/src/api_service.py`**

```python
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
        model_path = os.getenv("MODEL_PATH", os.path.join(os.path.dirname(__file__), "../../artifacts/gbdt"))
        schema_path = os.getenv("SCHEMA_PATH", os.path.join(os.path.dirname(__file__), "../../config/schema.json"))
        
        # Resolve absolute paths
        if not os.path.isabs(model_path):
            model_path = os.path.abspath(os.path.join(os.path.dirname(__file__), model_path))
        if not os.path.isabs(schema_path):
            schema_path = os.path.abspath(os.path.join(os.path.dirname(__file__), schema_path))
        
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
            if hasattr(model, 'decision_function'):
                try:
                    scores = model.decision_function(X_df)
                    probs = calibrator.predict_proba(scores)
                except:
                    probs = calibrator.predict_proba(probs)
            else:
                probs = calibrator.predict_proba(probs)
        
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
                    top_drivers = top_drivers_logistic(model, feature_names, X[i], top_k=2)
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
```

### Step 1.2: Fix .NET API Service Registration

**Update `src/api_dotnet/ClinicFlow.api/Program.cs`:**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register ML inference service with HttpClient
builder.Services.AddHttpClient<ClinicFlow.Api.Services.Inference.IInferenceService, 
    ClinicFlow.Api.Services.Inference.InferenceService>(client =>
{
    var mlUrl = builder.Configuration["MLService:BaseUrl"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(mlUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable CORS for web UI
app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => new { status = "ok", timestamp = DateTime.UtcNow })
    .WithName("GetHealth")
    .WithTags("Health");

app.Run();

public partial class Program { }
```

### Step 1.3: Update appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "MLService": {
    "BaseUrl": "http://localhost:8000"
  }
}
```

---

## Phase 2: Build Simple Web UI

### Step 2.1: Create Basic HTML/JavaScript Frontend

Create a simple single-page application:

**File: `src/api_dotnet/ClinicFlow.api/wwwroot/index.html`**

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>ClinicFlow - Schedule Optimization</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 20px;
        }
        .container {
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            border-radius: 12px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            overflow: hidden;
        }
        .header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px;
            text-align: center;
        }
        .header h1 { font-size: 2.5em; margin-bottom: 10px; }
        .header p { opacity: 0.9; }
        .content { padding: 30px; }
        .section { margin-bottom: 30px; }
        .section h2 { margin-bottom: 15px; color: #333; }
        .form-group { margin-bottom: 15px; }
        label { display: block; margin-bottom: 5px; font-weight: 600; color: #555; }
        input, select, textarea {
            width: 100%;
            padding: 10px;
            border: 2px solid #ddd;
            border-radius: 6px;
            font-size: 14px;
        }
        input:focus, select:focus, textarea:focus {
            outline: none;
            border-color: #667eea;
        }
        button {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            border: none;
            padding: 12px 30px;
            border-radius: 6px;
            font-size: 16px;
            font-weight: 600;
            cursor: pointer;
            transition: transform 0.2s;
        }
        button:hover { transform: translateY(-2px); }
        button:disabled { opacity: 0.6; cursor: not-allowed; }
        .results { margin-top: 30px; }
        .metric-card {
            background: #f8f9fa;
            padding: 20px;
            border-radius: 8px;
            margin-bottom: 15px;
        }
        .metric-card h3 { color: #667eea; margin-bottom: 10px; }
        .metric-value { font-size: 2em; font-weight: bold; color: #333; }
        table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 20px;
        }
        th, td {
            padding: 12px;
            text-align: left;
            border-bottom: 1px solid #ddd;
        }
        th { background: #f8f9fa; font-weight: 600; }
        .loading { text-align: center; padding: 40px; color: #667eea; }
        .error {
            background: #fee;
            color: #c33;
            padding: 15px;
            border-radius: 6px;
            margin: 15px 0;
        }
        .success {
            background: #efe;
            color: #3c3;
            padding: 15px;
            border-radius: 6px;
            margin: 15px 0;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>🏥 ClinicFlow</h1>
            <p>AI-Powered Schedule Optimization for Healthcare Clinics</p>
        </div>
        
        <div class="content">
            <!-- Clinic Profile Section -->
            <div class="section">
                <h2>Clinic Profile</h2>
                <div style="display: grid; grid-template-columns: repeat(2, 1fr); gap: 15px;">
                    <div class="form-group">
                        <label>Clinic Type</label>
                        <select id="clinicType">
                            <option value="PrimaryCare">Primary Care</option>
                            <option value="Dental">Dental</option>
                            <option value="PT">Physical Therapy</option>
                            <option value="Behavioral">Behavioral Health</option>
                            <option value="Other">Other</option>
                        </select>
                    </div>
                    <div class="form-group">
                        <label>Providers per Time Block</label>
                        <input type="number" id="providersPerBlock" value="3" min="1">
                    </div>
                    <div class="form-group">
                        <label>Average Service Minutes</label>
                        <input type="number" id="avgServiceMinutes" value="15" min="1">
                    </div>
                    <div class="form-group">
                        <label>Time Block Minutes</label>
                        <input type="number" id="timeBlockMinutes" value="240" min="1">
                    </div>
                </div>
            </div>

            <!-- Schedule Upload Section -->
            <div class="section">
                <h2>Schedule Upload</h2>
                <div class="form-group">
                    <label>Upload CSV or Paste JSON</label>
                    <input type="file" id="csvFile" accept=".csv" style="margin-bottom: 10px;">
                    <textarea id="jsonInput" placeholder='Paste JSON schedule here...' rows="8"></textarea>
                </div>
                <button onclick="loadSampleData()">📋 Load Sample Data</button>
                <button onclick="analyzeSchedule()" style="margin-left: 10px;">🔍 Analyze Schedule</button>
            </div>

            <!-- Results Section -->
            <div id="results" class="results" style="display: none;">
                <h2>Analysis Results</h2>
                <div id="summary"></div>
                <div id="predictions"></div>
                <div id="segments"></div>
            </div>

            <div id="loading" class="loading" style="display: none;">
                <p>⏳ Analyzing schedule...</p>
            </div>

            <div id="error"></div>
        </div>
    </div>

    <script>
        const API_BASE = window.location.origin;

        function loadSampleData() {
            const sampleData = {
                clinicProfile: {
                    clinicType: "PrimaryCare",
                    providersPerBlock: 3,
                    avgServiceMinutes: 15,
                    timeBlockMinutes: 240
                },
                appointments: [
                    {
                        appointmentId: 1,
                        appointmentType: "FollowUp",
                        dayOfWeek: "Tuesday",
                        timeBlock: "Morning",
                        leadTimeDays: 14,
                        isNewPatient: false,
                        historicalNoShowRate: 0.18,
                        unitRevenue: 120.0
                    },
                    {
                        appointmentId: 2,
                        appointmentType: "NewPatient",
                        dayOfWeek: "Tuesday",
                        timeBlock: "Morning",
                        leadTimeDays: 7,
                        isNewPatient: true,
                        historicalNoShowRate: 0.25,
                        unitRevenue: 150.0
                    },
                    {
                        appointmentId: 3,
                        appointmentType: "Procedure",
                        dayOfWeek: "Friday",
                        timeBlock: "Afternoon",
                        leadTimeDays: 21,
                        isNewPatient: false,
                        historicalNoShowRate: 0.15,
                        unitRevenue: 250.0
                    }
                ]
            };
            document.getElementById('jsonInput').value = JSON.stringify(sampleData, null, 2);
        }

        async function analyzeSchedule() {
            const loading = document.getElementById('loading');
            const results = document.getElementById('results');
            const errorDiv = document.getElementById('error');
            const summaryDiv = document.getElementById('summary');
            const predictionsDiv = document.getElementById('predictions');
            const segmentsDiv = document.getElementById('segments');

            loading.style.display = 'block';
            results.style.display = 'none';
            errorDiv.innerHTML = '';

            try {
                // Get clinic profile
                const clinicProfile = {
                    clinicType: document.getElementById('clinicType').value,
                    providersPerBlock: parseInt(document.getElementById('providersPerBlock').value),
                    avgServiceMinutes: parseInt(document.getElementById('avgServiceMinutes').value),
                    timeBlockMinutes: parseInt(document.getElementById('timeBlockMinutes').value)
                };

                // Get appointments
                let appointments;
                const csvFile = document.getElementById('csvFile').files[0];
                const jsonInput = document.getElementById('jsonInput').value.trim();

                if (csvFile) {
                    // Parse CSV
                    const csvText = await csvFile.text();
                    appointments = parseCSV(csvText);
                } else if (jsonInput) {
                    // Parse JSON
                    const data = JSON.parse(jsonInput);
                    appointments = data.appointments || data;
                } else {
                    throw new Error('Please upload a CSV file or paste JSON data');
                }

                // Build request
                const request = {
                    clinicProfile: clinicProfile,
                    appointments: appointments
                };

                // Call API
                const response = await fetch(`${API_BASE}/schedule/analyze`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify(request)
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Request failed');
                }

                const data = await response.json();

                // Display results
                displayResults(data);
                results.style.display = 'block';

            } catch (error) {
                errorDiv.innerHTML = `<div class="error">❌ Error: ${error.message}</div>`;
            } finally {
                loading.style.display = 'none';
            }
        }

        function parseCSV(csvText) {
            const lines = csvText.split('\n').filter(l => l.trim());
            const headers = lines[0].split(',').map(h => h.trim());
            const appointments = [];

            for (let i = 1; i < lines.length; i++) {
                const values = lines[i].split(',').map(v => v.trim());
                const apt = {};
                headers.forEach((header, idx) => {
                    let value = values[idx];
                    // Try to parse as number/boolean
                    if (value === 'true' || value === 'True') value = true;
                    else if (value === 'false' || value === 'False') value = false;
                    else if (!isNaN(value)) value = parseFloat(value);
                    apt[header] = value;
                });
                appointments.push(apt);
            }

            return appointments;
        }

        function displayResults(data) {
            // Summary metrics
            const summary = data.summary || data.baselineSummary;
            document.getElementById('summary').innerHTML = `
                <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 15px; margin-bottom: 30px;">
                    <div class="metric-card">
                        <h3>Expected Kept Appointments</h3>
                        <div class="metric-value">${summary.expectedKeptAppointments.toFixed(1)}</div>
                    </div>
                    <div class="metric-card">
                        <h3>Expected Revenue</h3>
                        <div class="metric-value">$${summary.baselineRevenue.toFixed(2)}</div>
                    </div>
                    <div class="metric-card">
                        <h3>Capacity</h3>
                        <div class="metric-value">${summary.blockCapacityAppointments.toFixed(1)}</div>
                    </div>
                    <div class="metric-card">
                        <h3>Overflow Risk</h3>
                        <div class="metric-value">${summary.expectedOverflowAppointments.toFixed(1)}</div>
                    </div>
                </div>
            `;

            // Predictions table
            if (data.slotPredictions && data.slotPredictions.length > 0) {
                let tableHTML = '<h3>No-Show Predictions</h3><table><thead><tr>';
                tableHTML += '<th>ID</th><th>Type</th><th>Day</th><th>Time</th><th>No-Show Prob</th><th>Confidence</th></tr></thead><tbody>';
                
                data.slotPredictions.slice(0, 20).forEach(pred => {
                    const apt = data.appointments?.find(a => a.appointmentId === pred.appointmentId);
                    tableHTML += `<tr>
                        <td>${pred.appointmentId}</td>
                        <td>${apt?.appointmentType || 'N/A'}</td>
                        <td>${apt?.dayOfWeek || 'N/A'}</td>
                        <td>${apt?.timeBlock || 'N/A'}</td>
                        <td>${(pred.noShowProbability * 100).toFixed(1)}%</td>
                        <td>${pred.confidenceInterval ? `${(pred.confidenceInterval[0]*100).toFixed(1)}-${(pred.confidenceInterval[1]*100).toFixed(1)}%` : 'N/A'}</td>
                    </tr>`;
                });
                
                tableHTML += '</tbody></table>';
                if (data.slotPredictions.length > 20) {
                    tableHTML += `<p style="margin-top: 10px; color: #666;">Showing first 20 of ${data.slotPredictions.length} appointments</p>`;
                }
                document.getElementById('predictions').innerHTML = tableHTML;
            }

            // Segments
            if (data.segments && data.segments.length > 0) {
                let segmentsHTML = '<h3>Segment Analysis</h3><table><thead><tr>';
                segmentsHTML += '<th>Segment</th><th>Expected Kept</th><th>Revenue</th><th>Capacity</th><th>Overflow</th><th>Status</th></tr></thead><tbody>';
                
                data.segments.forEach(seg => {
                    segmentsHTML += `<tr>
                        <td><strong>${seg.segmentKey}</strong></td>
                        <td>${seg.expectedKeptAppointments.toFixed(1)}</td>
                        <td>$${seg.baselineRevenue.toFixed(2)}</td>
                        <td>${seg.capacityAppointments.toFixed(1)}</td>
                        <td>${seg.expectedOverflowAppointments.toFixed(1)}</td>
                        <td>${seg.isOverCapacity ? '⚠️ Over' : '✅ OK'}</td>
                    </tr>`;
                });
                
                segmentsHTML += '</tbody></table>';
                document.getElementById('segments').innerHTML = segmentsHTML;
            }
        }

        // Load sample data on page load
        window.addEventListener('load', () => {
            loadSampleData();
        });
    </script>
</body>
</html>
```

### Step 2.2: Enable Static Files in .NET API

**Update `Program.cs` to serve static files:**

```csharp
// After builder setup, before app.Build():
builder.Services.AddDirectoryBrowser(); // Optional

// After app.Build(), before app.Run():
app.UseDefaultFiles(); // Serves index.html by default
app.UseStaticFiles();  // Serves static files from wwwroot
```

---

## Phase 3: End-to-End Testing

### Step 3.1: Start Services

**Terminal 1 - Start Python ML Service:**
```bash
cd src/ml/src
python3 api_service.py
# Should see: "✅ Model loaded: gbdt"
# Service running on http://localhost:8000
```

**Terminal 2 - Start .NET API:**
```bash
cd src/api_dotnet/ClinicFlow.api
dotnet run
# API running on http://localhost:5127
```

### Step 3.2: Test via Browser

1. Open browser to: `http://localhost:5127`
2. Click "Load Sample Data"
3. Click "Analyze Schedule"
4. View results

### Step 3.3: Test via API Directly

```bash
# Test health endpoint
curl http://localhost:5127/health

# Test analyze endpoint
curl -X POST http://localhost:5127/schedule/analyze \
  -H "Content-Type: application/json" \
  -d @test_request.json
```

---

## Phase 4: Quick Wins for Better UX

### Step 4.1: Add CSV Download Template

Create `wwwroot/sample_schedule.csv`:

```csv
appointment_type,day_of_week,time_block,lead_time_days,is_new_patient,historical_no_show_rate,unit_revenue
FollowUp,Tuesday,Morning,14,false,0.18,120.0
NewPatient,Tuesday,Morning,7,true,0.25,150.0
Procedure,Friday,Afternoon,21,false,0.15,250.0
```

Add download link in HTML:
```html
<a href="/sample_schedule.csv" download>📥 Download Sample CSV Template</a>
```

### Step 4.2: Add Visualization

Add a simple chart using Chart.js (CDN):

```html
<script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
```

Create charts for:
- No-show probability distribution
- Revenue by segment
- Capacity utilization

---

## Phase 5: Deployment Preparation

### Step 5.1: Create docker-compose.yml

```yaml
version: '3.8'

services:
  ml-service:
    build: ./src/ml
    ports:
      - "8000:8000"
    environment:
      - MODEL_PATH=/app/artifacts/gbdt
      - SCHEMA_PATH=/app/config/schema.json
    volumes:
      - ./src/ml/artifacts:/app/artifacts
      - ./src/ml/config:/app/config

  api:
    build: ./src/api_dotnet/ClinicFlow.api
    ports:
      - "5127:5127"
    environment:
      - MLService__BaseUrl=http://ml-service:8000
    depends_on:
      - ml-service
```

### Step 5.2: Test Locally with Docker

```bash
docker-compose up --build
```

---

## Next Steps Summary

1. ✅ **Create Python ML Service** (`api_service.py`)
2. ✅ **Fix .NET Service Registration** (Program.cs)
3. ✅ **Build Web UI** (index.html)
4. ✅ **Enable Static Files** (Program.cs)
5. ✅ **Test End-to-End**
6. ✅ **Deploy**

**You'll have a fully functional MVP with:**
- ✅ Web interface for users
- ✅ API endpoints working
- ✅ ML inference integrated
- ✅ Ready for deployment

---

## Troubleshooting

### ML Service won't start
- Check model path is correct
- Verify artifacts directory exists
- Check Python dependencies installed

### API can't reach ML service
- Verify ML service is running on port 8000
- Check CORS settings
- Verify MLService:BaseUrl in appsettings.json

### UI not loading
- Check static files middleware enabled
- Verify wwwroot directory exists
- Check browser console for errors

---

**Ready to build? Start with Step 1.1!** 🚀

