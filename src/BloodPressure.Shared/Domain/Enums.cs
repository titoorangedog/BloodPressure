namespace BloodPressure.Shared.Domain;

public enum Position
{
    Supine = 0,
    Sitting = 1,
    Standing = 2,
    Other = 3
}

public enum Severity
{
    VeryLow = 0,
    Low = 1,
    Normal = 2,
    High = 3,
    VeryHigh = 4
}

public enum ColorKey
{
    BlueViolet = 0,
    LightBlue = 1,
    Green = 2,
    Orange = 3,
    Red = 4
}

public enum UserRole
{
    User = 0,
    SuperUser = 1,
    Admin = 2
}

public enum LicenseType
{
    Free = 0,
    Advanced = 1,
    Premium = 2
}
