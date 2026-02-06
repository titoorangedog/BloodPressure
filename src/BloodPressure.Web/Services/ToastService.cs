namespace BloodPressure.Web.Services;

public enum ToastLevel
{
    Success,
    Error,
    Info
}

public sealed class ToastMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Message { get; init; } = string.Empty;
    public ToastLevel Level { get; init; } = ToastLevel.Info;
    public int TimeoutMs { get; init; } = 4000;
}

public sealed class ToastService
{
    private readonly List<ToastMessage> messages = new();

    public event Action? OnChange;

    public IReadOnlyList<ToastMessage> Messages => messages;

    public void Show(string message, ToastLevel level = ToastLevel.Info, int timeoutMs = 4000)
    {
        var toast = new ToastMessage
        {
            Message = message,
            Level = level,
            TimeoutMs = timeoutMs
        };

        messages.Add(toast);
        Notify();

        _ = AutoRemoveAsync(toast);
    }

    public void Remove(Guid id)
    {
        var index = messages.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            return;
        }

        messages.RemoveAt(index);
        Notify();
    }

    private async Task AutoRemoveAsync(ToastMessage toast)
    {
        if (toast.TimeoutMs <= 0)
        {
            return;
        }

        await Task.Delay(toast.TimeoutMs);
        Remove(toast.Id);
    }

    private void Notify() => OnChange?.Invoke();
}
