using System.Collections.Generic;
using System.Threading.Tasks;
using WiinUSoft.ViewModels;
using Xunit;

namespace Shared.Tests;

public class ViewModelInfrastructureTests
{
    [Fact]
    public void SetPropertyRaisesChangeOnlyWhenValueChanges()
    {
        var viewModel = new TestViewModel();
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        Assert.True(viewModel.SetName("Guitar"));
        Assert.False(viewModel.SetName("Guitar"));

        Assert.Equal("Guitar", viewModel.Name);
        Assert.Single(changedProperties);
        Assert.Equal(nameof(TestViewModel.Name), changedProperties[0]);
    }

    [Fact]
    public void RelayCommandUsesCanExecuteAndRaisesChange()
    {
        var canExecute = false;
        var executed = false;
        var commandRaised = false;
        var command = new RelayCommand(
            () => executed = true,
            () => canExecute);
        command.CanExecuteChanged += (_, _) => commandRaised = true;

        Assert.False(command.CanExecute(null));

        canExecute = true;
        command.NotifyCanExecuteChanged();
        command.Execute(null);

        Assert.True(commandRaised);
        Assert.True(command.CanExecute(null));
        Assert.True(executed);
    }

    [Fact]
    public async Task AsyncRelayCommandDisablesWhileRunning()
    {
        var allowCompletion = new TaskCompletionSource();
        var started = new TaskCompletionSource();
        var commandRaisedCount = 0;
        var command = new AsyncRelayCommand(async () =>
        {
            started.SetResult();
            await allowCompletion.Task;
        });
        command.CanExecuteChanged += (_, _) => commandRaisedCount++;

        var execution = command.ExecuteAsync();
        await started.Task;

        Assert.True(command.IsExecuting);
        Assert.False(command.CanExecute(null));

        allowCompletion.SetResult();
        await execution;

        Assert.False(command.IsExecuting);
        Assert.True(command.CanExecute(null));
        Assert.Equal(2, commandRaisedCount);
    }

    private sealed class TestViewModel : ViewModelBase
    {
        private string _name = string.Empty;

        public string Name => _name;

        public bool SetName(string name)
        {
            return SetProperty(ref _name, name, nameof(Name));
        }
    }
}
