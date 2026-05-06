using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace WiinUSoft
{
    /// <summary>
    /// Extension helpers that give WinUI 3 Window a ShowDialog-like async awaitable.
    /// </summary>
    internal static class WindowExtensions
    {
        /// <summary>
        /// Activates the window and returns a Task that completes when the window is closed.
        /// This mimics the blocking ShowDialog() behaviour of WPF in an async-safe way.
        /// </summary>
        public static Task ShowAsDialogAsync(this Window window)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            window.Closed += (s, e) => tcs.TrySetResult(true);
            window.Activate();
            return tcs.Task;
        }
    }
}
