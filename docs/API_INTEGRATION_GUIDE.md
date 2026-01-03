# API Integration & Interface Guide

Complete guide for integrating ML inference into the .NET API and building a usable interface.

---

## Table of Contents

1. [Complete ML Inference Integration](#1-complete-ml-inference-integration)
2. [Build Simple Web UI](#2-build-simple-web-ui)
3. [Test End-to-End](#3-test-end-to-end)
4. [Deployment Ready](#4-deployment-ready)

---

## 1. Complete ML Inference Integration

### Option A: Python Service (Easier - Recommended for MVP)

#### Step 1.1: Create Python FastAPI Service

Already created in `src/ml/src/api_service.py` from the operations guide. Update it to load the correct model path:

```python
# In api_service.py, update the startup:
@app.on_event("startup")
async def load_model():
    global model, preprocessor, calibrator, schema, saved_hash, feature_names
    
    # Use environment variable or default
    model_path = os.getenv("MODEL_PATH", "../../artifacts/gbdt")
    schema_path = os.getenv("SCHEMA_PATH", "../../config/schema.json")
    
    model, preprocessor, calibrator, saved_hash, meta = load_artifacts(model_path)
    schema = load_schema(schema_path)
    feature_names = list(preprocessor.get_feature_names_out())
```

#### Step 1.2: Start Python Service

```bash
cd src/ml/src
python3 api_service.py
# Service runs on http://localhost:8000
```

#### Step 1.3: Implement .NET Inference Service

Create the inference service in C#:

```csharp
// src/api_dotnet/ClinicFlow.api/Services/Inference/InferenceService.cs
using System.Net.Http.Json;
using ClinicFlow.Api.Dtos.Requests;
using ClinicFlow.Api.Dtos.Responses;

namespace ClinicFlow.Api.Services.Inference;

public class InferenceService : IInferenceService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InferenceService> _logger;
    private readonly string _mlServiceUrl;

    public InferenceService(HttpClient httpClient, ILogger<InferenceService> logger, IConfiguration config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _mlServiceUrl = config["MLService:BaseUrl"] ?? "http://localhost:8000";
        _httpClient.BaseAddress = new Uri(_mlServiceUrl);
    }

    public async Task<List<SlotPredictionStubDto>> PredictAsync(ScheduleRequestDto request, CancellationToken ct = default)
    {
        try
        {
            // Convert to Python service format
            var payload = new
            {
                clinic_profile = new
                {
                    clinic_type = request.ClinicProfile.ClinicType,
                    providers_per_block = request.ClinicProfile.ProvidersPerBlock,
                    avg_service_minutes = request.ClinicProfile.AvgServiceMinutes,
                    time_block_minutes = request.ClinicProfile.TimeBlockMinutes
                },
                appointments = request.Appointments.Select(a => new
                {
                    appointment_type = a.AppointmentType,
                    day_of_week = a.DayOfWeek,
                    time_block = a.TimeBlock,
                    lead_time_days = a.LeadTimeDays,
                    is_new_patient = a.IsNewPatient,
                    historical_no_show_rate = a.HistoricalNoShowRate,
                    unit_revenue = a.UnitRevenue
                }).ToList(),
                explain = true
            };

            var response = await _httpClient.PostAsJsonAsync("/predict", payload, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<PythonPredictionResponse>(ct);

            // Map back to DTOs
            return result!.predictions.Select((p, i) => new SlotPredictionStubDto(
                AppointmentId: request.Appointments[i].AppointmentId,
                NoShowProbability: p.no_show_probability
            )).ToList();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ML inference service unavailable");
            // Fallback to historical rates
            return request.Appointments.Select(a => new SlotPredictionStubDto(
                a.AppointmentId,
                a.HistoricalNoShowRate
            )).ToList();
        }
    }
}

// DTOs for Python service response
internal record PythonPredictionResponse(
    List<PythonPrediction> predictions,
    string? schema_hash,
    List<string> warnings
);

internal record PythonPrediction(
    double no_show_probability,
    double[] confidence_interval,
    List<string> top_drivers
);
```

```csharp
// src/api_dotnet/ClinicFlow.api/Services/Inference/IInferenceService.cs
namespace ClinicFlow.Api.Services.Inference;

using ClinicFlow.Api.Dtos.Requests;
using ClinicFlow.Api.Dtos.Responses;

public interface IInferenceService
{
    Task<List<SlotPredictionStubDto>> PredictAsync(ScheduleRequestDto request, CancellationToken ct = default);
}
```

#### Step 1.4: Register Service in Program.cs

```csharp
// Add to Program.cs
builder.Services.AddHttpClient<IInferenceService, InferenceService>(client =>
{
    var mlUrl = builder.Configuration["MLService:BaseUrl"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(mlUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Health check for ML service
builder.Services.AddHealthChecks()
    .AddCheck<MlServiceHealthCheck>("ml_service");
```

#### Step 1.5: Update ScheduleController to Use ML

```csharp
// Update ScheduleController.cs
public class ScheduleController : ControllerBase
{
    private readonly IInferenceService _inferenceService;

    public ScheduleController(IInferenceService inferenceService)
    {
        _inferenceService = inferenceService;
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] ScheduleRequestDto request)
    {
        var errors = ScheduleRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return BadRequest(new ErrorResponseDto("ValidationFailed", errors));
        }

        // Get ML predictions
        var slotPreds = await _inferenceService.PredictAsync(request);

        // Compute metrics using ML predictions instead of historical rates
        var (expectedKept, baselineRevenue) = BaselineMetricsCalculator.ComputeWithPredictions(request, slotPreds);
        
        // ... rest of the logic
    }
}
```

### Option B: ONNX Direct Integration (Better Performance)

For production, export models to ONNX and use ONNX Runtime in .NET:

1. Export model to ONNX (see ML_OPERATIONS_GUIDE.md)
2. Use `Microsoft.ML.OnnxRuntime` NuGet package
3. Load model directly in .NET (no Python service needed)

---

## 2. Build Simple Web UI

### Option A: Simple HTML/JavaScript (Quick Demo)

Create a basic web interface:

```html
<!-- src/frontend/index.html -->
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>ClinicFlow - Schedule Optimizer</title>
    <style>
        /* Modern, clean styling */
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
            background: #f5f5f5;
            padding: 2rem;
        }
        .container {
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            border-radius: 8px;
            padding: 2rem;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }
        h1 { color: #2563eb; margin-bottom: 2rem; }
        .section { margin-bottom: 2rem; }
        label { display: block; margin-bottom: 0.5rem; font-weight: 600; }
        input, select, textarea {
            width: 100%;
            padding: 0.75rem;
            border: 1px solid #ddd;
            border-radius: 4px;
            font-size: 14px;
        }
        button {
            background: #2563eb;
            color: white;
            padding: 0.75rem 2rem;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 16px;
            font-weight: 600;
        }
        button:hover { background: #1d4ed8; }
        button:disabled { background: #94a3b8; cursor: not-allowed; }
        .results {
            margin-top: 2rem;
            padding: 1.5rem;
            background: #f8fafc;
            border-radius: 4px;
            display: none;
        }
        .metric {
            display: inline-block;
            margin: 1rem;
            padding: 1rem;
            background: white;
            border-radius: 4px;
            min-width: 200px;
        }
        .metric-label { font-size: 12px; color: #64748b; }
        .metric-value { font-size: 24px; font-weight: bold; color: #1e293b; }
        .loading { display: none; text-align: center; padding: 2rem; }
    </style>
</head>
<body>
    <div class="container">
        <h1>🏥 ClinicFlow Schedule Optimizer</h1>
        
        <div class="section">
            <h2>Upload Schedule</h2>
            <label for="csvFile">CSV File:</label>
            <input type="file" id="csvFile" accept=".csv">
            <small>Or paste JSON below</small>
        </div>

        <div class="section">
            <h2>Clinic Profile</h2>
            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 1rem;">
                <div>
                    <label>Clinic Type:</label>
                    <select id="clinicType">
                        <option>PrimaryCare</option>
                        <option>Dental</option>
                        <option>PT</option>
                        <option>Behavioral</option>
                        <option>Other</option>
                    </select>
                </div>
                <div>
                    <label>Providers per Block:</label>
                    <input type="number" id="providersPerBlock" value="3" min="1">
                </div>
                <div>
                    <label>Avg Service Minutes:</label>
                    <input type="number" id="avgServiceMinutes" value="15" min="1">
                </div>
                <div>
                    <label>Time Block Minutes:</label>
                    <input type="number" id="timeBlockMinutes" value="240" min="1">
                </div>
            </div>
        </div>

        <div class="section">
            <label>Schedule JSON:</label>
            <textarea id="scheduleJson" rows="10" placeholder='{"appointments": [...]}'></textarea>
        </div>

        <button id="analyzeBtn" onclick="analyzeSchedule()">Analyze Schedule</button>
        <button id="optimizeBtn" onclick="optimizeSchedule()" style="margin-left: 1rem;">Optimize Strategy</button>

        <div class="loading" id="loading">
            <p>Processing...</p>
        </div>

        <div class="results" id="results"></div>
    </div>

    <script>
        const API_URL = 'https://localhost:7127'; // Update to your API URL

        async function analyzeSchedule() {
            const btn = document.getElementById('analyzeBtn');
            const loading = document.getElementById('loading');
            const results = document.getElementById('results');
            
            btn.disabled = true;
            loading.style.display = 'block';
            results.style.display = 'none';

            try {
                const request = buildRequest();
                const response = await fetch(`${API_URL}/schedule/analyze`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(request)
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'Request failed');
                }

                const data = await response.json();
                displayResults(data);
            } catch (error) {
                alert('Error: ' + error.message);
            } finally {
                btn.disabled = false;
                loading.style.display = 'none';
            }
        }

        async function optimizeSchedule() {
            // Similar to analyzeSchedule but calls /schedule/optimize
            // ... implementation
        }

        function buildRequest() {
            const scheduleJson = document.getElementById('scheduleJson').value;
            let appointments = [];
            
            try {
                const parsed = JSON.parse(scheduleJson);
                appointments = parsed.appointments || [];
            } catch (e) {
                throw new Error('Invalid JSON');
            }

            return {
                clinicProfile: {
                    clinicType: document.getElementById('clinicType').value,
                    providersPerBlock: parseInt(document.getElementById('providersPerBlock').value),
                    avgServiceMinutes: parseInt(document.getElementById('avgServiceMinutes').value),
                    timeBlockMinutes: parseInt(document.getElementById('timeBlockMinutes').value)
                },
                appointments: appointments
            };
        }

        function displayResults(data) {
            const results = document.getElementById('results');
            results.style.display = 'block';
            
            const html = `
                <h2>Analysis Results</h2>
                <div class="metric">
                    <div class="metric-label">Expected Kept Appointments</div>
                    <div class="metric-value">${data.summary.expectedKeptAppointments}</div>
                </div>
                <div class="metric">
                    <div class="metric-label">Baseline Revenue</div>
                    <div class="metric-value">$${data.summary.baselineRevenue.toFixed(2)}</div>
                </div>
                <div class="metric">
                    <div class="metric-label">Capacity</div>
                    <div class="metric-value">${data.summary.blockCapacityAppointments}</div>
                </div>
                <div class="metric">
                    <div class="metric-label">Overflow</div>
                    <div class="metric-value">${data.summary.expectedOverflowAppointments}</div>
                </div>
                <h3 style="margin-top: 2rem;">Segments</h3>
                <pre>${JSON.stringify(data.segments, null, 2)}</pre>
            `;
            
            results.innerHTML = html;
        }

        // CSV upload handler
        document.getElementById('csvFile').addEventListener('change', async (e) => {
            const file = e.target.files[0];
            if (!file) return;
            
            const text = await file.text();
            const lines = text.split('\n');
            const headers = lines[0].split(',');
            
            // Convert CSV to JSON format
            const appointments = lines.slice(1).filter(l => l.trim()).map(line => {
                const values = line.split(',');
                return {
                    appointmentId: parseInt(values[0]),
                    appointmentType: values[1],
                    dayOfWeek: values[2],
                    timeBlock: values[3],
                    leadTimeDays: parseInt(values[4]),
                    isNewPatient: values[5] === 'True',
                    historicalNoShowRate: parseFloat(values[6]),
                    unitRevenue: parseFloat(values[7])
                };
            });
            
            document.getElementById('scheduleJson').value = JSON.stringify({ appointments }, null, 2);
        });
    </script>
</body>
</html>
```

### Option B: Use Swagger UI (Easiest - Already Available)

The API already has Swagger configured! Just:

```bash
cd src/api_dotnet/ClinicFlow.api
dotnet run
# Navigate to https://localhost:7127/swagger
```

You can test all endpoints directly in the browser!

---

## 3. Quick Start: Make It Usable Now

### Step 3.1: Test API with Swagger (5 minutes)

```bash
cd src/api_dotnet/ClinicFlow.api
dotnet run
```

Then:
1. Open https://localhost:7127/swagger
2. Test `/schedule/analyze` with sample JSON
3. View interactive API documentation

### Step 3.2: Create Sample Request File

```json
// src/api_dotnet/ClinicFlow.api/test_request.json
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
      "appointmentType": "NewPatient",
      "dayOfWeek": "Monday",
      "timeBlock": "Morning",
      "leadTimeDays": 7,
      "isNewPatient": true,
      "historicalNoShowRate": 0.25,
      "unitRevenue": 150.0
    },
    {
      "appointmentId": 2,
      "appointmentType": "FollowUp",
      "dayOfWeek": "Monday",
      "timeBlock": "Morning",
      "leadTimeDays": 14,
      "isNewPatient": false,
      "historicalNoShowRate": 0.15,
      "unitRevenue": 100.0
    }
  ]
}
```

Test it:
```bash
curl -X POST http://localhost:5127/schedule/analyze \
  -H "Content-Type: application/json" \
  -d @test_request.json | python3 -m json.tool
```

---

## 4. Implementation Priority

### Phase 1: Make It Testable (1-2 hours)
1. ✅ API endpoints exist
2. ✅ Swagger UI available
3. ⚠️ Integrate ML inference (replace stubs)
4. ⚠️ Test with real predictions

### Phase 2: Simple Web UI (2-4 hours)
1. Create HTML interface
2. Add CSV upload
3. Display results nicely
4. Add basic charts

### Phase 3: Production UI (Optional)
1. React/Vue/Blazor frontend
2. Advanced visualizations
3. Export reports
4. Save configurations

---

## Recommended Next Steps

**For MVP Demo:**

1. **Use Swagger UI** (Already works!)
   - Test all endpoints
   - Share Swagger URL for demo
   - Document with examples

2. **Integrate ML Service** (1-2 hours)
   - Implement `IInferenceService`
   - Connect to Python FastAPI service
   - Replace stubs in controller

3. **Simple HTML Interface** (2-3 hours)
   - Basic upload & display
   - No framework needed
   - Good enough for demo

**Priority Order:**
1. ✅ Test API with Swagger (do this first)
2. ⚠️ Integrate ML inference (highest value)
3. ⚠️ Build simple HTML UI (for demo)
4. ⚠️ Production frontend (later)

Would you like me to implement the ML inference integration first, or create the HTML interface?

