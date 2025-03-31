using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Wpf;
using OxyPlot.Axes; // Importar el espacio de nombres para DateTimeAxis
using Microsoft.Win32;
using System.Windows.Threading;
using System.Windows.Controls; // Add this using directive for SelectionChangedEventArgs
using System.Threading;
using System.Threading.Tasks;
using ListBox = System.Windows.Controls.ListBox; // Resolve ambiguous reference for ListBox
using Microsoft.Extensions.Logging;
using Path = System.IO.Path; // Resolve ambiguous reference for Path
using Application = System.Windows.Application;
using System.Diagnostics;
using System; // Resolve ambiguous reference for Application
using System.Windows.Media.Animation;
using System.Security.Principal;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.ComponentModel;
using Microsoft.Win32.SafeHandles; // Añadido
using OxyPlot.Annotations; // Add this using directive for LineAnnotation

namespace PlayerDetector-Kill-SC-v1-EN;

// ...existing code...
public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow> _logger;
    private FileSystemWatcher? logWatcher;
    private string? logDirectory;
    private PerformanceMonitor performanceMonitor;
    private long _lastFilePosition = 0; // Mantén la última posición leída en el archivo
    private readonly object _lock = new object();
    private SaveFileDialog saveFileDialog = new SaveFileDialog(); // Inicializar saveFileDialog
    private bool autoScroll = true; // Añadido para controlar el auto-scroll
    private string currentLogPath = string.Empty;
    private CancellationTokenSource _cts;
    private Debouncer debouncer = new Debouncer(TimeSpan.FromMilliseconds(500));
    private IProgress<double> progressReporter;
    private const int PageSize = 1000;
    private long _currentPage = 0;
    private bool _fullLogLoaded = false;
    private readonly ObservableCollection<string> _logEntries = new(); // Change this line
    private HashSet<string> deathEventsSet = new HashSet<string>();
    private HashSet<string> errorEventsSet = new HashSet<string>();

    public MainWindow(ILogger<MainWindow> logger)
    {
        InitializeComponent();
        InitializeLoadingCircle();
        _logger = logger;
        CreateChart();
        performanceMonitor = new PerformanceMonitor();
        StartPerformanceMonitoring();
        StartLoadingSpinner();
        progressReporter = new Progress<double>(value => pbLoading.Value = value);
        logStarCitizenListBox.ItemsSource = _logEntries;
        _cts = new CancellationTokenSource(); // Initialize _cts

        // Inicializar estados de botones
        UpdateButtonStates(selectedButton: null);
        currentLogPath = string.Empty; // Initialize currentLogPath

        // Ensure the window is visible
        this.Visibility = Visibility.Visible;
        this.Show();
    }

    private void UpdateButtonStates(Button? selectedButton)
    {
        var defaultColor = (Color)ColorConverter.ConvertFromString("#FF0093B5");
        
        if (btnLive != null)
        {
            btnLive.Background = selectedButton == btnLive 
                ? new SolidColorBrush(Colors.LimeGreen) 
                : new SolidColorBrush(defaultColor);
        }
        
        if (btnPtu != null)
        {
            btnPtu.Background = selectedButton == btnPtu 
                ? new SolidColorBrush(Colors.LimeGreen) 
                : new SolidColorBrush(defaultColor);
        }
    }

    private void StartPerformanceMonitoring()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (s, e) => UpdatePerformance();
        timer.Start();
    }

    private void StartLoadingSpinner()
    {
        var rotateAnimation = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = TimeSpan.FromSeconds(1),
            RepeatBehavior = RepeatBehavior.Forever
        };
        SpinnerRotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
    }

    // ...existing code...
    private void ShowLoadingSpinner()
    {
        Dispatcher.Invoke(() =>
        {
            LoadingGrid.Visibility = Visibility.Visible;
            MainTabControl.SelectedIndex = 3;  // Cambiar a pestaña de log
            UpdateLayout();  // Forzar actualización de UI
            loadingTextBlock.Text = "LOADING LOG...";
            loadingTextBlock.FontFamily = (FontFamily)Application.Current.Resources["CalculatorFont"];
        });
    }

    private void HideLoadingSpinner()
    {
        Dispatcher.Invoke(() =>
        {
            LoadingGrid.Visibility = Visibility.Collapsed;
        });
    }

    private void LogError(string message)
    {
        Debug.WriteLine($"ERROR: {message}");
        File.AppendAllText("errors.log", $"{DateTime.Now}: {message}\n");
    }

    // ...existing code...
    private void UpdatePerformance()
    {
        var snapshot = performanceMonitor.GetCurrentSnapshot();
        Dispatcher.Invoke(() =>
        {
            // Update CPU display with temperature and usage
            cpuTempTextBlock.Text = $"{snapshot.CPUTemperature:0}°";
            cpuTextBlock.Text = $"{snapshot.CPUUsage:0.0}%";
            ramTextBlock.Text = snapshot.RAMUsage;
            gpuTempTextBlock.Text = $"{snapshot.GPUTemperature:0}°"; // Add this line
            gpuTextBlock.Text = $"{snapshot.GPUUsage:0.0}%";

            // Change color based on temperature
            if (snapshot.CPUTemperature > 65)
            {
                cpuTempTextBlock.Foreground = new SolidColorBrush(Colors.Red);
            }
            else
            {
                cpuTempTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 136)); // Green
            }

            var plotModel = performanceChart.Model;
            if (plotModel != null)
            {
                var cpuSeries = plotModel.Series[0] as LineSeries;
                var gpuSeries = plotModel.Series[1] as LineSeries;
                var ramSeries = plotModel.Series[2] as LineSeries;

                if (cpuSeries != null && gpuSeries != null && ramSeries != null)
                {
                    var now = DateTimeAxis.ToDouble(DateTime.Now);
                    cpuSeries.Points.Add(new DataPoint(now, snapshot.CPUUsage));
                    gpuSeries.Points.Add(new DataPoint(now, snapshot.GPUUsage));

                    // Handle invalid RAMUsage format gracefully
                    var ramUsageParts = snapshot.RAMUsage.Split('/');
                    if (ramUsageParts.Length == 2 && double.TryParse(ramUsageParts[0], out double usedRam))
                    {
                        ramSeries.Points.Add(new DataPoint(now, usedRam));
                    }
                    else
                    {
                        // Si no se puede obtener un valor válido, añadir un punto con valor cero o el último válido
                        double lastValue = ramSeries.Points.Count > 0 ?
                            ramSeries.Points[ramSeries.Points.Count - 1].Y : 0;
                        ramSeries.Points.Add(new DataPoint(now, lastValue));
                        Debug.WriteLine($"Invalid RAMUsage format: {snapshot.RAMUsage}");
                    }

                    // Limite el número de puntos para mantener el rendimiento
                    if (cpuSeries.Points.Count > 100)
                    {
                        cpuSeries.Points.RemoveAt(0);
                        gpuSeries.Points.RemoveAt(0);
                        ramSeries.Points.RemoveAt(0);
                    }

                    // Actualizar ejes para mostrar los últimos 60 segundos de datos
                    if (plotModel.Axes.Count >= 1 && plotModel.Axes[0] is DateTimeAxis timeAxis)
                    {
                        var latestTime = DateTime.Now;
                        var earliestTime = latestTime.AddSeconds(-60);

                        timeAxis.Minimum = DateTimeAxis.ToDouble(earliestTime);
                        timeAxis.Maximum = DateTimeAxis.ToDouble(latestTime);
                    }

                    // Actualizar el gráfico
                    plotModel.InvalidatePlot(true);
                }
            }
        });
    }

    private async Task ProcesarLogExistenteAsync(bool append = false)
    {
        if (string.IsNullOrEmpty(currentLogPath)) return;

        const int maxRetries = 5;
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                using var fs = new FileStream(
                    currentLogPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite, // Compartir acceso
                    bufferSize: 4096,
                    FileOptions.Asynchronous
                );

                using var reader = new StreamReader(fs);
                string? line; // Allow null values

                if (!append)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _logEntries.Clear();
                    });
                }

                while (!reader.EndOfStream)
                {
                    line = await reader.ReadLineAsync();
                    if (line == null) continue;

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _logEntries.Add(line);
                        ProcesarLineaLog(line); // Procesar eventos
                    });
                }

                return;
            }
            catch (IOException ex) when (IsFileLocked(ex))
            {
                retryCount++;
                await Task.Delay(1000 * retryCount); // Espera exponencial
            }
            catch (Exception ex)
            {
                LogError($"Fatal error: {ex.ToString()}");
                return;
            }
        }

        ShowMessageBox("Could not read the file. Is it being used by another program?");
    }

    private bool IsFileLocked(IOException ex)
    {
        int errorCode = ex.HResult & 0xFFFF;
        return errorCode == 32 || errorCode == 33; // ERROR_SHARING_VIOLATION o ERROR_LOCK_VIOLATION
    }

    private void InitializeLogWatcher()
    {
        logWatcher?.Dispose();

        if (string.IsNullOrEmpty(currentLogPath) || !File.Exists(currentLogPath)) return;

        try
        {
            logWatcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(currentLogPath) ?? string.Empty, // Ensure non-null value
                Filter = Path.GetFileName(currentLogPath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
                InternalBufferSize = 65536 // Buffer grande para evitar pérdidas
            };

            logWatcher.Changed += async (s, e) =>
            {
                await Task.Delay(500); // Esperar a que se libere el archivo
                await ProcesarLogExistenteAsync(append: true);
            };
        }
        catch (Exception ex)
        {
            LogError($"Error en FileSystemWatcher: {ex.ToString()}");
        }
    }

    private void ProcesarLineaLog(string line)
    {
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Añadir línea al ListBox
                if (_logEntries.Count > 5000) _logEntries.RemoveAt(0);
                _logEntries.Add(line);

                // Auto-scroll
                if (autoScroll) logStarCitizenListBox.ScrollIntoView(line);

                // Procesar eventos
                DetectKillEvents(line);
                ProcessErrorLine(line);
            }, DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            LogError($"Error en UI: {ex.ToString()}");
        }
    }

    private void DetectKillEvents(string logLine)
    {
        var regex = new Regex(@"<(?<timestamp>.*?)> \[Notice\] <Actor Death> CActor::Kill: '(?<victimName>.*?)' \[(?<victimId>\d+)\] in zone '(?<zone>.*?)' killed by '(?<killerName>.*?)' \[(?<killerId>\d+)\] using '(?<weaponName>.*?)' \[Class (?<weaponClass>.*?)\] with damage type '(?<damageType>.*?)' from direction x: (?<dirX>.*?), y: (?<dirY>.*?), z: (?<dirZ>.*?) \[(?<team>.*?)\]");
        
        var match = regex.Match(logLine);
        if (match.Success)
        {
            // Crear DeathEvent y añadir a la lista
            var death = new DeathEvent(
                logLine,
                match.Groups["timestamp"].Value,
                match.Groups["victimName"].Value,
                match.Groups["victimId"].Value,
                match.Groups["zone"].Value,
                match.Groups["killerName"].Value,
                match.Groups["killerId"].Value,
                match.Groups["weaponName"].Value,
                match.Groups["weaponClass"].Value,
                "N/A", // Placeholder para weaponId
                match.Groups["damageType"].Value,
                match.Groups["dirX"].Value,
                match.Groups["dirY"].Value,
                match.Groups["dirZ"].Value,
                match.Groups["team"].Value
            );

            // Evitar duplicados
            if (!deathEventsSet.Contains(death.LogLine))
            {
                deathEventsSet.Add(death.LogLine);

                // Crear un nuevo botón para el evento de muerte
                var button = new System.Windows.Controls.Button
                {
                    Content = $"{death.Timestamp}: damage type: {death.DamageType}",
                    Tag = death,
                    Style = (Style)FindResource("DeathEventButtonStyle") // Apply the new style
                };
                button.Click += DeathEventButton_Click;
                Dispatcher.Invoke(() => deathListBox.Items.Add(button));
            }
        }
    }

    private async Task LoadLogPage(long pageNumber)
    {
        if (string.IsNullOrEmpty(currentLogPath)) return;

        try
        {
            ShowLoadingSpinner();
            var lines = new List<string>();
            using (var fs = new FileStream(currentLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs))
            {
                long skipLines = pageNumber * PageSize;
                for (long i = 0; i < skipLines && !reader.EndOfStream; i++)
                {
                    await reader.ReadLineAsync();
                }

                for (long i = 0; i < PageSize && !reader.EndOfStream; i++)
                {
                    lines.Add(await reader.ReadLineAsync());
                }
            }

            foreach (var line in lines)
            {
                ProcesarLineaLog(line);
            }
        }
        catch (Exception ex)
        {
            LogError($"Error loading more lines: {ex.Message}");
        }
        finally
        {
            HideLoadingSpinner();
        }
    }

    // ...existing code...
    private void CreateChart()
    {
        // Create a modern-looking chart with a clean design
        var plotModel = new PlotModel
        {
            Title = string.Empty,
            PlotAreaBorderThickness = new OxyThickness(1), // Add border around the plot area
            PlotAreaBorderColor = OxyColor.FromRgb(70, 70, 70), // Subtle border color
            PlotMargins = new OxyThickness(30, 0, 10, 10), // Further increase left margin for better visibility
            Background = OxyColor.FromRgb(32, 75, 86) // Set background color of plot area
        };

        // Define series with modern colors
        var cpuSeries = new LineSeries
        {
            Title = "CPU",
            Color = OxyColor.FromRgb(0, 255, 136),
            StrokeThickness = 2,
            MarkerType = MarkerType.None
        };

        var gpuSeries = new LineSeries
        {
            Title = "GPU",
            Color = OxyColor.FromRgb(255, 184, 0),
            StrokeThickness = 2,
            MarkerType = MarkerType.None
        };

        var ramSeries = new LineSeries
        {
            Title = "RAM",
            Color = OxyColor.FromRgb(255, 61, 61),
            StrokeThickness = 2,
            MarkerType = MarkerType.None
        };

        plotModel.Series.Add(cpuSeries);
        plotModel.Series.Add(gpuSeries);
        plotModel.Series.Add(ramSeries);

        // Configure horizontal axis (time) with enhanced grid
        var timeAxis = new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            StringFormat = "HH:mm:ss",
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.FromRgb(70, 70, 70), // More visible grid lines
            MajorGridlineThickness = 1,
            MinorGridlineStyle = LineStyle.Dot,
            MinorGridlineColor = OxyColor.FromRgb(50, 50, 50),
            MinorGridlineThickness = 0.5,
            TicklineColor = OxyColor.FromRgb(100, 100, 100),
            TextColor = OxyColor.FromRgb(200, 200, 200),
            AxislineColor = OxyColor.FromRgb(100, 100, 100),
            AxislineStyle = LineStyle.Solid,
            AxislineThickness = 1.5,
            IntervalLength = 80 // Adjust the spacing of grid lines
        };

        // Configure vertical axis (values) with enhanced grid
        var valueAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.FromRgb(70, 70, 70), // More visible grid lines
            MajorGridlineThickness = 1,
            MinorGridlineStyle = LineStyle.Dot,
            MinorGridlineColor = OxyColor.FromRgb(50, 50, 50),
            MinorGridlineThickness = 0.5,
            MajorStep = 20, // Show values at 0, 20, 40, 60, 80, 100
            MinorStep = 10, // Add minor ticks at 10, 30, 50, 70, 90
            TicklineColor = OxyColor.FromRgb(100, 100, 100),
            Minimum = 0, // Ensure the Y-axis starts at 0
            Maximum = 100,
            IsAxisVisible = true,
            AxislineColor = OxyColor.FromRgb(100, 100, 100),
            AxislineStyle = LineStyle.Solid,
            AxislineThickness = 1.5,
            Title = "", // Remove title
            TextColor = OxyColor.FromRgb(200, 200, 200),
            IntervalLength = 50, // Adjust the spacing of grid lines
            AxisTitleDistance = 10, // Adjust distance of axis title from axis line
            AxisTickToLabelDistance = 15 // Further increase distance of axis labels from axis ticks
        };

        plotModel.Axes.Add(timeAxis);
        plotModel.Axes.Add(valueAxis);

        // Add reference lines at 25%, 50%, and 75% for better readability
        var referenceLine25 = new LineAnnotation
        {
            Type = LineAnnotationType.Horizontal,
            Y = 25,
            Color = OxyColor.FromArgb(60, 150, 150, 150),
            LineStyle = LineStyle.Dash
        };

        var referenceLine50 = new LineAnnotation
        {
            Type = LineAnnotationType.Horizontal,
            Y = 50,
            Color = OxyColor.FromArgb(60, 150, 150, 150),
            LineStyle = LineStyle.Dash
        };

        var referenceLine75 = new LineAnnotation
        {
            Type = LineAnnotationType.Horizontal,
            Y = 75,
            Color = OxyColor.FromArgb(60, 150, 150, 150),
            LineStyle = LineStyle.Dash
        };

        plotModel.Annotations.Add(referenceLine25);
        plotModel.Annotations.Add(referenceLine50);
        plotModel.Annotations.Add(referenceLine75);

        // Enable legend for better user understanding
        plotModel.IsLegendVisible = true;

        // Set the model to the chart control
        performanceChart.Model = plotModel;
        performanceChart.Controller = new PlotController();

        // Disable all interactions
        performanceChart.Controller.BindMouseDown(OxyMouseButton.Left, null);
        performanceChart.Controller.BindMouseDown(OxyMouseButton.Right, null);
        performanceChart.Controller.BindMouseDown(OxyMouseButton.Middle, null);
        performanceChart.Controller.BindMouseWheel(null);

        // Set size and appearance
        performanceChart.Width = 380;
        performanceChart.Height = 250;
        performanceChart.Background = new SolidColorBrush(Color.FromRgb(32, 75, 86)); // Color de fondo más oscuro para mejor contraste
    }

    private void InitializeLoadingCircle()
    {
        // Define los puntos del círculo
        Ellipse[] circlePoints = new Ellipse[8];
        for (int i = 0; i < 8; i++)
        {
            var ellipse = new Ellipse
            {
                Width = 15,
                Height = 15,
                Fill = new SolidColorBrush(Color.FromRgb(0, 147, 181)),
                Opacity = 0.2 // Opacidad inicial
            };

            // Posición alrededor del círculo
            double angle = i * 45; // 45° entre puntos
            double x = Math.Cos(angle * Math.PI / 180) * 40; // Radio
            double y = Math.Sin(angle * Math.PI / 180) * 40;

            ellipse.RenderTransform = new TranslateTransform(x, y);
            circlePoints[i] = ellipse;

            // Agregar el punto al contenedor de la interfaz
            LoadingGrid.Children.Add(ellipse);
        }

        // Animar los puntos
        StartCircleLoadingAnimation(circlePoints);
    }

    private void StartCircleLoadingAnimation(Ellipse[] circlePoints)
    {
        var storyboard = new Storyboard();

        for (int i = 0; i < circlePoints.Length; i++)
        {
            var fadeAnimation = new DoubleAnimation
            {
                From = 0.2, // Opacidad inicial
                To = 1.0,  // Opacidad máxima
                Duration = TimeSpan.FromSeconds(0.8), // Duración de cada animación
                BeginTime = TimeSpan.FromMilliseconds(i * 100), // Escalonar el inicio
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            Storyboard.SetTarget(fadeAnimation, circlePoints[i]);
            Storyboard.SetTargetProperty(fadeAnimation, new PropertyPath(Ellipse.OpacityProperty));
            storyboard.Children.Add(fadeAnimation);
        }

        storyboard.Begin();
    }

    private void ShowMessageBox(string message)
    {
        var messageBox = new Window
        {
            Content = new TextBlock { Text = message, Margin = new Thickness(10) },
            Width = 300,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = this.Left - 310, // Position to the left of MainWindow with a margin
            Top = this.Top
        };
        messageBox.ShowDialog();
    }

    private void ProcessErrorLine(string line)
    {
        if (line.Contains("ERROR") || line.Contains("WARNING"))
        {
            string errorType = line.Contains("ERROR") ? "ERROR" : line.Contains("WARNING") ? "WARNING" : "FLOW";

            // Extract timestamp from the log line
            string timestamp = ExtractTimestamp(line);

            // Avoid duplicates
            if (!errorEventsSet.Contains(line))
            {
                errorEventsSet.Add(line);

                var button = new System.Windows.Controls.Button
                {
                    Content = $"{errorType}: {timestamp}",
                    Tag = line,
                    Style = (Style)FindResource("ErrorEventButtonStyle") // Apply the updated style
                };
                button.Click += ErrorEventButton_Click;
                Dispatcher.Invoke(() => errorListBox.Items.Add(button));
            }
        }
    }

    private void DeathEventButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is DeathEvent selectedDeath)
        {
            string details = $"Date and time of the event: {selectedDeath.Timestamp}\n\n" +
                             $"Dead actor:\nName: {selectedDeath.VictimName}\nID: {selectedDeath.VictimId}\n\n" +
                             $"Event arena:\nLocation: {selectedDeath.Zone}\n\n" +
                             $"Cause of death:\nKiller:\nName: {selectedDeath.KillerName}\nID: {selectedDeath.KillerId}\n\n" +
                             $"Weapon used:\nName: {selectedDeath.WeaponName}\nClase: {selectedDeath.WeaponClass}\nID: {selectedDeath.WeaponId}\n" +
                             $"Damage type: {selectedDeath.DamageType}\n\n" +
                             $"Direction of attack:\nVector coordinates:\nx: {selectedDeath.DirX}, y: {selectedDeath.DirY}, z: {selectedDeath.DirZ}\n\n" +
                             $"Additional metadata:\nTeam involved: {selectedDeath.Team}\n";


            // Crear un botón "Guardar"
            var saveButton = new System.Windows.Controls.Button
            {
                Content = "Save",
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFBF7F7"))
            };
            saveButton.Click += (s, args) =>
            {
                SaveDeathEventToFile(selectedDeath);
            };

            // Crear un StackPanel para contener los detalles y el botón "Guardar"
            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock { Text = details, Margin = new Thickness(10, 10, 0, 0) }); // Add margin to the TextBlock
            stackPanel.Children.Add(saveButton);

            // Mostrar los detalles y el botón "Guardar" en un MessageBox
            var window = new Window
            {
                Content = stackPanel,
                Width = 400,
                Height = 415,
                Title = "Details of the death event"
            };

            // Posicionar la ventana emergente al lado derecho de la ventana principal en cascada
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            int offset = 30 * (Application.Current.Windows.Count - 1); // Calcular el desplazamiento en cascada
            window.Left = this.Left + this.Width + 10 + offset; // 10 es el margen de separación
            window.Top = this.Top + offset;

            window.Show();
        }
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Handle tab selection change if needed
    }

    private void deathListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (deathListBox.SelectedItem is DeathEvent selectedDeath)
        {
            // Mostrar detalles de la muerte
            var details = $"Details of death:\n{selectedDeath}";
            MessageBox.Show(details);
        }
    }

    private string ExtractTimestamp(string line)
    {
        // Assuming the timestamp is at the beginning of the line and follows a specific format
        var match = Regex.Match(line, @"<(?<timestamp>.*?)>");
        return match.Success ? match.Groups["timestamp"].Value : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void SaveDeathEventToFile(DeathEvent deathEvent)
    {
        var dialog = new SaveFileDialog
        {
            FileName = $"DeathEvent_{deathEvent.Timestamp:yyyyMMdd_HHmmss}.txt",
            Filter = "Text files (*.txt)|*.txt",
            Title = "Save event of death"
        };

        if (dialog.ShowDialog() == true)
        {
            var details = $"Date and time of the event: {deathEvent.Timestamp}\n\n" +
                          $"Dead actor: nNumber: {deathEvent.VictimName} nID: {deathEvent.VictimId}\n\n" +
                          $"Area of the event: nLocation: {deathEvent.Zone}\n\n" +
                          $"Cause of death: nKiller: nNom: {deathEvent.KillerName} nID: {deathEvent.KillerId}\n\n" +
                          $"Weapon used: nNumber: {deathEvent.WeaponName} nClase: {deathEvent.WeaponClass} nID: {deathEvent.WeaponId}\n" +
                          $"Type of damage: {deathEvent.DamageType}\n\n" +
                          $"Direction of attack: nVector coordinates: nx: {deathEvent.DirX}, y: {deathEvent.DirY}, z: {deathEvent.DirZ}\n\n" +
                          $"Additional metadata: nEnterprise team: {deathEvent.Team}\n";

            File.WriteAllText(dialog.FileName, details);
        }
    }

    private void ErrorEventButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string errorLine)
        {
            string details = GetErrorDetails(errorLine);
            MessageBox.Show(details, "Detalles del error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string GetErrorDetails(string errorLine)
    {
        string details = string.Empty;

        if (errorLine.Contains("[FLOW]"))
        {
            details =
        }
        else if (errorLine.Contains("[ERROR]"))
        {
          
        }
        else if (errorLine.Contains("[WARNING]"))
        {
           
        }

        return details;
    }

    private async void LoadLogButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button == null) return;

        try
        {
            ShowLoadingSpinner();
            string logType = button.Content.ToString();
            string basePath = logType == "LIVE" 
                ? @"C:\Program Files\Roberts Space Industries\StarCitizen\LIVE" 
                : @"C:\Program Files\Roberts Space Industries\StarCitizen\PTU";

            currentLogPath = Path.Combine(basePath, "Game.log");
            
            // Verificar existencia del archivo
            if (!File.Exists(currentLogPath))
            {
                ShowMessageBox($"Game.log not found in: {currentLogPath}");
                return;
            }

            // Limpiar datos anteriores de forma segura
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _logEntries.Clear();
                deathListBox.Items.Clear();
                errorListBox.Items.Clear();
                deathEventsSet.Clear();
                errorEventsSet.Clear();
            });

            // Forzar actualización inicial
            await ProcesarLogExistenteAsync();
            
            // Iniciar monitoreo en tiempo real
            InitializeLogWatcher();
            
            UpdateButtonStates(button);
        }
        catch (Exception ex)
        {
            LogError($"Critical Error: {ex.ToString()}");
            ShowMessageBox($"Error while loading log: {ex.Message}");
        }
        finally
        {
            HideLoadingSpinner();
        }
    }

    private async void SelectCustomLog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            InitialDirectory = @"C:\Program Files\Roberts Space Industries\StarCitizen",
            Filter = "Log files (*.log)|*.log",
            Title = "Select Game.log"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                ShowLoadingSpinner();
                
                // Resetear todo el estado
                _cts?.Cancel();
                logWatcher?.Dispose();
                
                currentLogPath = dialog.FileName;
                
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _logEntries.Clear();
                    deathListBox.Items.Clear();
                    errorListBox.Items.Clear();
                    deathEventsSet.Clear();
                    errorEventsSet.Clear();
                });

                await ProcesarLogExistenteAsync();
                InitializeLogWatcher();
            }
            catch (Exception ex)
            {
                LogError($"Error manual load: {ex.ToString()}");
                MessageBox.Show($"Error: {ex.Message}");
            }
            finally
            {
                HideLoadingSpinner();
            }
        }
    }

    private async void ReloadLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(currentLogPath)) return;

        try
        {
            ShowLoadingSpinner();
            await ProcesarLogExistenteAsync(append: true); // Método completamente asíncrono
        }
        catch (Exception ex)
        {
            LogError($"Error when reloading: {ex.Message}");
        }
        finally
        {
            HideLoadingSpinner();
        }
    }

    private async void btnLoadMore_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            btnLoadMore.IsEnabled = false;
            await LoadLogPage(_currentPage++);
        }
        finally
        {
            btnLoadMore.IsEnabled = true;
        }
    }

    private void DonateButton_Click(object sender, RoutedEventArgs e)
    {
        var url = "";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LogError($"Error opening donation link: {ex.Message}");
            ShowMessageBox("The donation link could not be opened.");
        }
    }

    private void OpenRulesOfConduct_Click(object sender, RoutedEventArgs e)
    {
        var url = "https://robertsspaceindustries.com/en/tos#rules_of_conduct";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LogError($"Error opening rules of conduct link: {ex.Message}");
            ShowMessageBox("The rules of conduct link could not be opened.");
        }
    }

    private void OpenReport_Click(object sender, RoutedEventArgs e)
    {
        var url = "https://support.robertsspaceindustries.com/hc/en-us/requests/new?ticket_form_id=360000658153";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LogError($"Error opening report link: {ex.Message}");
            ShowMessageBox("The report link could not be opened.");
        }
    }

    private void OpenReportFile_Click(object sender, RoutedEventArgs e)
    {
        string reportFilePath = "Report.txt";
        if (!File.Exists(reportFilePath))
        {
            File.WriteAllText(reportFilePath, @"

To report a player in Star Citizen, you have two main options:

1️ In-game:
If the user is committing a real-time violation, you can report it in-game using the reporting system:
1. Open the global chat (F12 to open it if it's hidden)
2. Type /report [username] [reason] and send the message
Example:
/report Player123 Chat Harassment

2️ Via a support ticket:
If the issue is more serious (cheating, persistent harassment, toxic behavior, etc.), you should submit a ticket on the RSI support website: Clicking the REPORT button in the app will link you directly to the support website.

The most appropriate ticket type is Gameplay Issues, as this is an in-game issue. In the ticket, please include:
- Name of the offending player
- Date and time of the incident
- Description of the issue
- Screenshots or videos as evidence (if possible)

If it's something very serious, such as cheating or bug exploitation, you can also report it on:
- The official forums
- The official Star Citizen Discord

Useful links related to Star Citizen:

1. [Star Citizen Wiki](https://starcitizen.tools/Community_Links): A comprehensive source of information about the game, including guides and community links.

2. [Star Citizen Tools](https://starcitizen.today/helpfullinks/): A specialized wiki offering tools and information about the Star Citizen universe.

3. [Starship 42](https://starcitizen.today/helpfullinks/): Explore and manage your fleet of ships with interactive tools.

4. [Erkul Games](https://starcitizen.today/helpfullinks/): Optimize your ship configurations and experiment with different equipment.

5. [SC Trade Tools](https://www.reddit.com/r/starcitizen/comments/18pvjes/list_of_useful_star_citizen_links_now_with_a/): Tools for in-game trading.

6. [Galactic Logistics](https://www.reddit.com/r/starcitizen/comments/18pvjes/list_of_useful_star_citizen_links_now_with_a/): Information on trade routes and mining.

7. [RSI Main Page](https://www.reddit.com/r/starcitizen/comments/18pvjes/list_of_useful_star_citizen_links_now_with_a/): Official Roberts Space Industries page for news and updates.

I hope you find these links useful for your adventure in the Star Citizen universe.🚀
");
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = reportFilePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LogError($"Error opening report file: {ex.Message}");
            ShowMessageBox("Report file could not be opened.");
        }
    }

    private void DisableAdminFeatures()
    {
        // Deshabilitar aquí las funciones que requieren permisos de administrador
    }
}
// Copyright (c) 2024 DoxData. Exclusive ownership. 
// Prohibited use/modification without express authorization.
