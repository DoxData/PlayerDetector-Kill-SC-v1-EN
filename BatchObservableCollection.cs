using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows;

namespace PlayerDetector-Kill-SC-v1-EN
{
    public class BatchObservableCollection<T> : ObservableCollection<T>
    {
        private long _lastFilePosition;
        private string currentLogPath;

        public void AddRange(IEnumerable<T> items)
        {
            CheckReentrancy();

            foreach (var item in items)
                Items.Add(item);

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));
        }

        private void ProcesarLogExistente()
        {
            try
            {
                using FileStream fs = new(currentLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);

                // Leer desde el inicio del archivo
                fs.Seek(0, SeekOrigin.Begin);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Application.Current.Dispatcher.Invoke(() => ProcesarLineaLog(line));
                }
                _lastFilePosition = fs.Position;
            }
            catch (Exception ex)
            {
                LogError($"Error processing log: {ex.Message}");
            }
        }

        private void LogError(string message)
        {
            // Implement proper error logging
            System.Diagnostics.Debug.WriteLine($"ERROR: {message}");
            File.AppendAllText("errors.log", $"{DateTime.Now}: {message}\n");
        }

        private void ProcesarLineaLog(string line)
        {
            // Implementation would be specific to your application
            // This is just a placeholder
        }
    }
}