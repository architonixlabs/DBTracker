namespace DBTracker.Web.Services;

public record ToastMessage(string Text, bool IsError = false);

/// <summary>Scoped per-circuit toast bus. ToastHost in MainLayout subscribes and renders.</summary>
public class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void Success(string text) => OnShow?.Invoke(new ToastMessage(text));

    public void Error(string text) => OnShow?.Invoke(new ToastMessage(text, IsError: true));
}
