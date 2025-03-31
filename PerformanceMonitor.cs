using LibreHardwareMonitor.Hardware;
using System.Diagnostics;
using System.Management;
using System.IO;
using System;
using System.Runtime.InteropServices;

namespace PlayerDetector-Kill-SC-v1-EN
{
    public struct PerformanceSnapshot
    {
        public float CPUUsage { get; init; }
        public float CPUTemperature { get; init; }
        public float GPUUsage { get; init; }
        public float GPUTemperature { get; init; } // Add this line
        public string RAMUsage { get; init; }
    }

    public class PerformanceMonitor : IDisposable
    {
        private Computer _computer = new Computer();
        private ISensor? _cpuLoadSensor;
        private ISensor? _cpuTempSensor;
        private ISensor? _gpuLoadSensor;
        private ISensor? _gpuTempSensor; // Add this line

        public PerformanceMonitor()
        {
            _computer.IsCpuEnabled = true;
            _computer.IsGpuEnabled = true;
            _computer.Open();
            InitializeSensors();
        }

        private void InitializeSensors()
        {
            Debug.WriteLine("Inicializando sensores...");

            foreach (var hardware in _computer.Hardware)
            {
                try
                {
                    hardware.Update();
                    Debug.WriteLine($"Hardware detectado: {hardware.HardwareType} - {hardware.Name}");

                    // Detectar CPU
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        foreach (var sensor in hardware.Sensors)
                        {
                            Debug.WriteLine($"Sensor CPU: {sensor.Name} ({sensor.SensorType})");
                            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("CPU Total"))
                            {
                                _cpuLoadSensor = sensor;
                                Debug.WriteLine("Sensor CPU Total seleccionado");
                            }
                            // Detect CPU temperature sensor (prefer Package or Core temp)
                            if (sensor.SensorType == SensorType.Temperature &&
                               (sensor.Name.Contains("Package") || sensor.Name.Contains("Core #1")))
                            {
                                _cpuTempSensor = sensor;
                                Debug.WriteLine($"Sensor de temperatura CPU seleccionado: {sensor.Name}");
                            }
                        }

                        // If we didn't find the preferred temp sensors, use any CPU temp sensor
                        if (_cpuTempSensor == null)
                        {
                            foreach (var sensor in hardware.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Temperature)
                                {
                                    _cpuTempSensor = sensor;
                                    Debug.WriteLine($"Sensor de temperatura alternativo seleccionado: {sensor.Name}");
                                    break;
                                }
                            }
                        }
                    }

                    // Detectar GPU
                    if (hardware.HardwareType.ToString().Contains("Gpu"))
                    {
                        foreach (var sensor in hardware.Sensors)
                        {
                            Debug.WriteLine($"Sensor GPU: {sensor.Name} ({sensor.SensorType})");
                            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("GPU Core"))
                            {
                                _gpuLoadSensor = sensor;
                                Debug.WriteLine("Sensor GPU Core seleccionado");
                            }
                            // Detect GPU temperature sensor
                            if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("GPU Core"))
                            {
                                _gpuTempSensor = sensor;
                                Debug.WriteLine($"Sensor de temperatura GPU seleccionado: {sensor.Name}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error inicializando hardware {hardware.Name}: {ex.Message}");
                }
            }
        }

        public PerformanceSnapshot GetCurrentSnapshot()
        {
            // Ensure sensors are updated before retrieving values
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
            }

            Debug.WriteLine($"CPU Load Sensor Value: {_cpuLoadSensor?.Value}");
            Debug.WriteLine($"CPU Temp Sensor Value: {_cpuTempSensor?.Value}");
            Debug.WriteLine($"GPU Load Sensor Value: {_gpuLoadSensor?.Value}");

            return new PerformanceSnapshot
            {
                CPUUsage = (float)(_cpuLoadSensor?.Value ?? 0),
                CPUTemperature = (float)(_cpuTempSensor?.Value ?? 0),
                GPUUsage = (float)(_gpuLoadSensor?.Value ?? 0),
                GPUTemperature = (float)(_gpuTempSensor?.Value ?? 0), // Add this line
                RAMUsage = GetRamUsage()
            };
        }

        private bool IsTemperatureSensorAvailable()
        {
            return _cpuTempSensor != null && _cpuTempSensor.Value.HasValue;
        }

        private void ForceUpdateSensors()
        {
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
            }
        }

        private void LogSensorValues()
        {
            foreach (var hardware in _computer.Hardware)
            {
                foreach (var sensor in hardware.Sensors)
                {
                    Debug.WriteLine($"Sensor: {sensor.Name}, Type: {sensor.SensorType}, Value: {sensor.Value}");
                }
            }
        }

        private string GetRamUsage()
        {
            try
            {
                // Obtener memoria en bytes usando PerformanceCounter
                using var availableCounter = new PerformanceCounter("Memory", "Available Bytes");
                float availableBytes = availableCounter.NextValue();
                float totalBytes = GetTotalPhysicalMemory();

                // Validar que los valores de memoria sean válidos
                if (totalBytes <= 0)
                {
                    // Intentar método alternativo directo con GlobalMemoryStatusEx
                    var memoryStatus = new NativeMemoryStatus();
                    if (GetMemoryStatusWithNative(ref memoryStatus))
                    {
                        totalBytes = memoryStatus.TotalPhys;
                        availableBytes = memoryStatus.AvailPhys;
                    }
                    else
                    {
                        Debug.WriteLine("Error: No se pudo obtener información de memoria con métodos alternativos");
                        return "N/A GB"; // Formato de error más claro
                    }
                }

                // Convertir a GB
                float availableGB = availableBytes / (1024 * 1024 * 1024);
                float totalGB = totalBytes / (1024 * 1024 * 1024);
                float usedGB = totalGB - availableGB;

                // Validación adicional para evitar valores negativos o inconsistentes
                if (usedGB < 0 || usedGB > totalGB)
                {
                    Debug.WriteLine($"Valores de memoria inconsistentes: usado={usedGB}, total={totalGB}");
                    usedGB = Math.Max(0, Math.Min(usedGB, totalGB));
                }

                Debug.WriteLine($"RAM Usage: {usedGB:F1}/{totalGB:F1} GB");
                return $"{Math.Round(usedGB)}/{Math.Round(totalGB)} GB"; // Sin decimales
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error obteniendo uso de RAM: {ex.Message}");

                // Intentar método de respaldo con Win32
                try
                {
                    var memoryStatus = new NativeMemoryStatus();
                    if (GetMemoryStatusWithNative(ref memoryStatus))
                    {
                        float totalGB = memoryStatus.TotalPhys / (1024 * 1024 * 1024);
                        float availableGB = memoryStatus.AvailPhys / (1024 * 1024 * 1024);
                        float usedGB = totalGB - availableGB;

                        return $"{Math.Round(usedGB)}/{Math.Round(totalGB)} GB";
                    }
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine($"Error en método alternativo: {ex2.Message}");
                }

                return "N/A GB"; // Formato simplificado para errores
            }
        }

        private float GetTotalPhysicalMemory()
        {
            try
            {
                // Método 1: Win32_PhysicalMemory
                using var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
                var collection = searcher.Get();

                // Verificar si se obtuvieron resultados
                if (collection.Count == 0)
                {
                    Debug.WriteLine("No se encontraron módulos de memoria física");
                    // Intentamos método alternativo dentro de la misma función
                    return GetTotalPhysicalMemoryAlternative();
                }

                float totalMemory = collection
                    .Cast<ManagementObject>()
                    .Sum(m => Convert.ToSingle(m["Capacity"]));

                if (totalMemory <= 0)
                {
                    Debug.WriteLine("Win32_PhysicalMemory devolvió valor inválido");
                    return GetTotalPhysicalMemoryAlternative();
                }

                Debug.WriteLine($"Total Physical Memory: {totalMemory / (1024 * 1024 * 1024):F2} GB");
                return totalMemory;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error obteniendo memoria física total: {ex.Message}");
                return GetTotalPhysicalMemoryAlternative();
            }
        }

        private float GetTotalPhysicalMemoryAlternative()
        {
            try
            {
                // Método 2: GlobalMemoryStatusEx via P/Invoke
                var memoryStatus = new NativeMemoryStatus();
                if (GetMemoryStatusWithNative(ref memoryStatus))
                {
                    Debug.WriteLine($"Memoria total (método alternativo): {memoryStatus.TotalPhys / (1024 * 1024 * 1024):F2} GB");
                    return memoryStatus.TotalPhys;
                }

                // Método 3: PerformanceCounter directamente
                using var ramCounter = new PerformanceCounter("Memory", "Commit Limit");
                var commitLimit = ramCounter.NextValue();
                Debug.WriteLine($"Commit Limit (PerformanceCounter): {commitLimit / (1024 * 1024 * 1024):F2} GB");
                return commitLimit;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en método alternativo para obtener memoria física: {ex.Message}");
                return 0;
            }
        }

        // Estructura para P/Invoke
        [StructLayout(LayoutKind.Sequential)]
        public struct NativeMemoryStatus
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;

            public NativeMemoryStatus()
            {
                Length = (uint)Marshal.SizeOf(typeof(NativeMemoryStatus));
            }
        }

        // Importar función nativa
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref NativeMemoryStatus lpBuffer);

        private bool GetMemoryStatusWithNative(ref NativeMemoryStatus status)
        {
            try
            {
                return GlobalMemoryStatusEx(ref status);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en GlobalMemoryStatusEx: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _computer.Close();
        }
    }

    public class PerformanceLogger : IDisposable
    {
        private readonly Stopwatch _sw;
        private readonly string _operationName;
        private readonly StreamWriter _writer;

        public PerformanceLogger(string operationName)
        {
            _operationName = operationName;
            _sw = Stopwatch.StartNew();
            _writer = new StreamWriter("performance.log", true);
        }

        public void LogProgress(int linesProcessed)
        {
            _writer.WriteLine($"[{DateTime.Now:o}] {_operationName} - " +
                             $"Tiempo: {_sw.Elapsed.TotalMilliseconds}ms, " +
                             $"Líneas: {linesProcessed}, " +
                             $"Memoria: {GC.GetTotalMemory(false) / 1024}KB");
        }

        public void Dispose()
        {
            _sw.Stop();
            _writer.WriteLine($"[{DateTime.Now:o}] {_operationName} - " +
                             $"Total: {_sw.Elapsed.TotalMilliseconds}ms");
            _writer.Dispose();
        }
    }
}
// Copyright (c) 2024 DoxData. Exclusive ownership. 
// Prohibited use/modification without express authorization.
