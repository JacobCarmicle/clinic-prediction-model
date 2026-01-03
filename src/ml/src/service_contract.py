# request/response shapes used by API
# src/service_contract.py
"""
Service contract definitions matching .NET API DTOs.

These dataclasses mirror the C# DTOs to ensure consistent data structures
between the Python ML service and the .NET API layer.
"""
from __future__ import annotations
from dataclasses import dataclass, field
from typing import List, Optional, Dict, Any, Literal, Tuple

# ---- Type Aliases (matching API enums) ----

AppointmentType = Literal["NewPatient", "FollowUp", "Procedure", "Lab", "Other"]
DayOfWeek = Literal["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"]
TimeBlock = Literal["Morning", "Midday", "Afternoon", "Evening"]
ClinicSpecialty = Literal["PrimaryCare", "Dental", "PT", "Behavioral", "Other"]

# ---- Input DTOs (matching API request structure) ----

@dataclass
class ClinicProfileDto:
    """Clinic operational profile for simulation."""
    clinic_type: ClinicSpecialty
    providers_per_block: int
    avg_service_minutes: int
    time_block_minutes: int  # Required in .NET API
    # Optional fields (if API accepts them)
    # clinic_specialty: Optional[ClinicSpecialty] = None

@dataclass
class AppointmentSlotDto:
    """
    Single appointment slot with anonymized features.
    
    Matches AppointmentSlotDto.cs structure:
    - appointment_id: Optional identifier (may not be in all requests)
    - All required fields from schema
    """
    appointment_type: AppointmentType
    day_of_week: DayOfWeek
    time_block: TimeBlock
    lead_time_days: int
    is_new_patient: bool
    historical_no_show_rate: float
    unit_revenue: float
    # Optional fields (from .NET DTO or schema)
    appointment_id: Optional[int] = None  # From .NET DTO
    clinic_specialty: Optional[ClinicSpecialty] = None
    slot_capacity: Optional[int] = None

@dataclass
class InferenceRequestDto:
    """
    Complete inference request matching API structure.
    
    Matches ScheduleRequestDto.cs for /schedule/analyze endpoint.
    """
    clinic_profile: ClinicProfileDto
    appointments: List[AppointmentSlotDto]
    # Optional flag for explainability (may be in query params, not body)
    explain: bool = True


# ---- Output DTOs ----

@dataclass
class InferencePerSlotDto:
    no_show_probability: float
    confidence_interval: Tuple[float, float]
    top_drivers: List[str] = field(default_factory=list)

@dataclass
class InferenceResponseDto:
    predictions: List[InferencePerSlotDto]
    # Optional model metadata (handy for debugging / UI)
    model_version: Optional[str] = None
    schema_hash: Optional[str] = None
    warnings: List[str] = field(default_factory=list)


# ---- Serialization helpers ----

def to_dict(obj: Any) -> Any:
    """
    Convert dataclass to JSON-serializable dict.
    
    Recursively handles nested dataclasses, lists, and tuples.
    Used for converting Python DTOs to JSON for API responses.
    
    Args:
        obj: Dataclass instance, list, tuple, or primitive
    
    Returns:
        JSON-serializable dict/list/primitive
    """
    if hasattr(obj, "__dataclass_fields__"):
        d = {}
        for k in obj.__dataclass_fields__.keys():
            value = getattr(obj, k)
            d[k] = to_dict(value)
        return d
    if isinstance(obj, list):
        return [to_dict(x) for x in obj]
    if isinstance(obj, tuple):
        return list(obj)  # Convert tuple to list for JSON
    return obj


def from_dict_inference_request(payload: Dict[str, Any]) -> InferenceRequestDto:
    """
    Deserialize JSON payload to InferenceRequestDto.
    
    Validates required fields and constructs DTOs from dictionary.
    Used for parsing API request bodies.
    
    Args:
        payload: Dictionary from JSON request body
    
    Returns:
        InferenceRequestDto instance
    
    Raises:
        KeyError: If required fields are missing
        TypeError: If field types don't match
        ValueError: If enum values are invalid
    """
    if "clinic_profile" not in payload:
        raise KeyError("Missing required field: clinic_profile")
    if "appointments" not in payload:
        raise KeyError("Missing required field: appointments")
    
    try:
        cp = payload["clinic_profile"]
        clinic_profile = ClinicProfileDto(**cp)
        
        appointments = [
            AppointmentSlotDto(**a)
            for a in payload["appointments"]
        ]
        
        return InferenceRequestDto(
            clinic_profile=clinic_profile,
            appointments=appointments,
            explain=bool(payload.get("explain", True))
        )
    except TypeError as e:
        raise ValueError(f"Invalid field type in request payload: {e}") from e
    except Exception as e:
        raise ValueError(f"Failed to deserialize request: {e}") from e
