namespace Kontainr.Services;

public class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void Success(string message) => Show(message, ToastType.Success);
    public void Error(string message) => Show(message, ToastType.Error);
    public void Info(string message) => Show(message, ToastType.Info);
    public void Warning(string message) => Show(message, ToastType.Warning);

    private void Show(string message, ToastType type) =>
        OnShow?.Invoke(new ToastMessage(message, type, DateTime.UtcNow));
}

public record ToastMessage(string Message, ToastType Type, DateTime Created);

public enum ToastType { Success, Error, Info, Warning }
