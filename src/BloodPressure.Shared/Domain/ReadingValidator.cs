using BloodPressure.Shared.Contracts;

namespace BloodPressure.Shared.Domain;

public static class ReadingValidator
{
    public static bool TryValidate(ReadingCreateRequest request, out string error)
    {
        if (request.Systolic is < ValidationRules.SystolicMin or > ValidationRules.SystolicMax)
        {
            error = $"Systolic must be between {ValidationRules.SystolicMin} and {ValidationRules.SystolicMax}.";
            return false;
        }

        if (request.Diastolic is < ValidationRules.DiastolicMin or > ValidationRules.DiastolicMax)
        {
            error = $"Diastolic must be between {ValidationRules.DiastolicMin} and {ValidationRules.DiastolicMax}.";
            return false;
        }

        if (request.Diastolic >= request.Systolic)
        {
            error = "Diastolic must be less than systolic.";
            return false;
        }

        if (request.HeartRate is < ValidationRules.HeartRateMin or > ValidationRules.HeartRateMax)
        {
            error = $"Heart rate must be between {ValidationRules.HeartRateMin} and {ValidationRules.HeartRateMax}.";
            return false;
        }

        if (request.WeightKg is < ValidationRules.WeightMin or > ValidationRules.WeightMax)
        {
            error = $"Weight must be between {ValidationRules.WeightMin} and {ValidationRules.WeightMax}.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
