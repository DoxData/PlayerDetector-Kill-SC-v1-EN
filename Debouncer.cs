using System;
using System.Threading.Tasks;

namespace PlayerDetector-Kill-SC-v1-EN
{
    /// <summary>
    /// Utility class that prevents multiple executions of an action within a specified time interval.
    /// </summary>
    public class Debouncer
    {
        private readonly TimeSpan _interval;
        private DateTime _lastInvokeTime = DateTime.MinValue;

        /// <summary>
        /// Initializes a new instance of the Debouncer class.
        /// </summary>
        /// <param name="interval">The minimum time interval between allowed action executions.</param>
        public Debouncer(TimeSpan interval) => _interval = interval;

        /// <summary>
        /// Executes the specified action only if enough time has passed since the last execution.
        /// </summary>
        /// <param name="action">The asynchronous action to execute.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task DebounceAsync(Func<Task> action)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastInvokeTime) < _interval) return;
            
            _lastInvokeTime = now;
            await action();
        }
    }
}
