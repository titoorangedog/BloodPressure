namespace BloodPressure.Shared.Domain;

public static class ValidationRules
{
    public const int SystolicMin = 50;
    public const int SystolicMax = 260;
    public const int DiastolicMin = 30;
    public const int DiastolicMax = 160;
    public const int HeartRateMin = 30;
    public const int HeartRateMax = 220;
    public const decimal WeightMin = 20m;
    public const decimal WeightMax = 400m;
}
