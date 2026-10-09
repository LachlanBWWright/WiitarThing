using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WiinUSoft.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private readonly Action<Exception>? _onError;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null, Action<Exception>? onError = null)
        : this(
            _ => execute(),
            canExecute is null ? null : _ => canExecute(),
            onError)
    {
        ArgumentNullException.ThrowIfNull(execute);
    }

    public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null, Action<Exception>? onError = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _onError = onError;
    }

    public event EventHandler? CanExecuteChanged;

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (_isExecuting == value)
                return;

            _isExecuting = value;
            NotifyCanExecuteChanged();
        }
    }

    public bool CanExecute(object? parameter)
    {
        return !IsExecuting && (_canExecute?.Invoke(parameter) ?? true);
    }

    public async void Execute(object? parameter)
    {
        await ExecuteAsync(parameter);
    }

    public async Task ExecuteAsync(object? parameter = null)
    {
        if (!CanExecute(parameter))
            return;

        try
        {
            IsExecuting = true;
            await _execute(parameter).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _onError?.Invoke(exception);
            ExecutionFailed?.Invoke(this, exception);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public event EventHandler<Exception>? ExecutionFailed;

    public void NotifyCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
