using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Serilog;

namespace KimiAppNative.Analytics
{
    /// <summary>
    /// Monitors application performance and system resources
    /// </summary>
    public class PerformanceMonitor : IDisposable
    {
        private readonly Timer _monitoringTimer;
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        private readonly Process _currentProcess;
        private readonly Queue<PerformanceSnapshot> _snapshots;
        private readonly int _maxSnapshots = 1000;
        private readonly string _performanceLogPath;
        private readonly ILogger _logger;
        private bool _isMonitoring;

        public bool IsMonitoring => _isMonitoring;
        public PerformanceMetrics CurrentMetrics { get; private set; }
        public PerformanceStatistics Statistics { get; private set; }

        public event EventHandler<PerformanceSnapshot>? SnapshotCaptured;
        public event EventHandler<PerformanceAlert>? AlertRaised;
        public event EventHandler<PerformanceMetrics>? MetricsUpdated;

        public PerformanceMonitor()
        {
            _currentProcess = Process.GetCurrentProcess();
            _snapshots = new Queue<PerformanceSnapshot>();
            
            _performanceLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OhGee",
                "Performance",
                "logs"
            );
            
            Directory.CreateDirectory(_performanceLogPath);
            
            // Initialize performance counters
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            
            // Initialize logger
            _logger = new LoggerConfiguration()
                .WriteTo.File(
                    Path.Combine(_performanceLogPath, "performance-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30)
                .CreateLogger();
            
            CurrentMetrics = new PerformanceMetrics();
            Statistics = new PerformanceStatistics();
            
            // Start monitoring timer
            _monitoringTimer = new Timer(CaptureSnapshot, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            _isMonitoring = true;
        }

        /// <summary>
        /// Captures a performance snapshot
        /// </summary>
        private void CaptureSnapshot(object? state)
        {
            try
            {
                var snapshot = new PerformanceSnapshot
                {
                    Timestamp = DateTime.Now,
                    CpuUsage = GetCpuUsage(),
                    MemoryUsage = GetMemoryUsage(),
                    ProcessMemory = _currentProcess.WorkingSet64 / (1024 * 1024), // MB
                    ThreadCount = _currentProcess.Threads.Count,
                    HandleCount = _currentProcess.HandleCount,
                    GCMemory = GC.GetTotalMemory(false) / (1024 * 1024), // MB
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2)
                };
                
                // Add to queue
                lock (_snapshots)
                {
                    _snapshots.Enqueue(snapshot);
                    
                    // Maintain max snapshots
                    while (_snapshots.Count > _maxSnapshots)
                    {
                        _snapshots.Dequeue();
                    }
                }
                
                // Update current metrics
                UpdateMetrics(snapshot);
                
                // Check for alerts
                CheckAlerts(snapshot);
                
                // Raise event
                SnapshotCaptured?.Invoke(this, snapshot);
                
                // Log if needed
                if (snapshot.CpuUsage > 80 || snapshot.MemoryUsage > 80)
                {
                    _logger.Warning("High resource usage detected: CPU={CpuUsage}%, Memory={MemoryUsage}%",
                        snapshot.CpuUsage, snapshot.MemoryUsage);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error capturing performance snapshot");
            }
        }

        /// <summary>
        /// Gets current CPU usage
        /// </summary>
        private float GetCpuUsage()
        {
            try
            {
                return _cpuCounter.NextValue();
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets current memory usage percentage
        /// </summary>
        private float GetMemoryUsage()
        {
            try
            {
                var totalMemory = GetTotalPhysicalMemory();
                var availableMemory = _memoryCounter.NextValue();
                var usedMemory = totalMemory - (availableMemory * 1024 * 1024);
                return (float)(usedMemory / totalMemory * 100);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets total physical memory
        /// </summary>
        private long GetTotalPhysicalMemory()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return Convert.ToInt64(obj["TotalPhysicalMemory"]);
                }
            }
            catch
            {
                // Fallback
            }
            
            return 8L * 1024 * 1024 * 1024; // Default 8GB
        }

        /// <summary>
        /// Updates current metrics
        /// </summary>
        private void UpdateMetrics(PerformanceSnapshot snapshot)
        {
            CurrentMetrics.LastUpdate = snapshot.Timestamp;
            CurrentMetrics.CurrentCpuUsage = snapshot.CpuUsage;
            CurrentMetrics.CurrentMemoryUsage = snapshot.MemoryUsage;
            CurrentMetrics.CurrentProcessMemory = snapshot.ProcessMemory;
            
            // Calculate averages
            lock (_snapshots)
            {
                if (_snapshots.Any())
                {
                    CurrentMetrics.AverageCpuUsage = _snapshots.Average(s => s.CpuUsage);
                    CurrentMetrics.AverageMemoryUsage = _snapshots.Average(s => s.MemoryUsage);
                    CurrentMetrics.PeakCpuUsage = _snapshots.Max(s => s.CpuUsage);
                    CurrentMetrics.PeakMemoryUsage = _snapshots.Max(s => s.MemoryUsage);
                    CurrentMetrics.PeakProcessMemory = _snapshots.Max(s => s.ProcessMemory);
                }
            }
            
            MetricsUpdated?.Invoke(this, CurrentMetrics);
        }

        /// <summary>
        /// Checks for performance alerts
        /// </summary>
        private void CheckAlerts(PerformanceSnapshot snapshot)
        {
            var alerts = new List<PerformanceAlert>();
            
            // CPU alert
            if (snapshot.CpuUsage > 90)
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.HighCpu,
                    Severity = AlertSeverity.Critical,
                    Message = $"Critical CPU usage: {snapshot.CpuUsage:F1}%",
                    Value = snapshot.CpuUsage,
                    Timestamp = snapshot.Timestamp
                });
            }
            else if (snapshot.CpuUsage > 75)
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.HighCpu,
                    Severity = AlertSeverity.Warning,
                    Message = $"High CPU usage: {snapshot.CpuUsage:F1}%",
                    Value = snapshot.CpuUsage,
                    Timestamp = snapshot.Timestamp
                });
            }
            
            // Memory alert
            if (snapshot.MemoryUsage > 90)
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.HighMemory,
                    Severity = AlertSeverity.Critical,
                    Message = $"Critical memory usage: {snapshot.MemoryUsage:F1}%",
                    Value = snapshot.MemoryUsage,
                    Timestamp = snapshot.Timestamp
                });
            }
            else if (snapshot.MemoryUsage > 80)
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.HighMemory,
                    Severity = AlertSeverity.Warning,
                    Message = $"High memory usage: {snapshot.MemoryUsage:F1}%",
                    Value = snapshot.MemoryUsage,
                    Timestamp = snapshot.Timestamp
                });
            }
            
            // Process memory alert
            if (snapshot.ProcessMemory > 1000) // > 1GB
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.HighProcessMemory,
                    Severity = AlertSeverity.Warning,
                    Message = $"High process memory: {snapshot.ProcessMemory:F0} MB",
                    Value = snapshot.ProcessMemory,
                    Timestamp = snapshot.Timestamp
                });
            }
            
            // Raise alerts
            foreach (var alert in alerts)
            {
                AlertRaised?.Invoke(this, alert);
                _logger.Warning("Performance alert: {AlertMessage}", alert.Message);
            }
        }

        /// <summary>
        /// Gets performance report
        /// </summary>
        public PerformanceReport GenerateReport(TimeSpan period)
        {
            var report = new PerformanceReport
            {
                StartTime = DateTime.Now.Subtract(period),
                EndTime = DateTime.Now,
                TotalSnapshots = 0
            };
            
            lock (_snapshots)
            {
                var relevantSnapshots = _snapshots
                    .Where(s => s.Timestamp >= report.StartTime)
                    .ToList();
                
                if (relevantSnapshots.Any())
                {
                    report.TotalSnapshots = relevantSnapshots.Count;
                    report.AverageCpuUsage = relevantSnapshots.Average(s => s.CpuUsage);
                    report.AverageMemoryUsage = relevantSnapshots.Average(s => s.MemoryUsage);
                    report.PeakCpuUsage = relevantSnapshots.Max(s => s.CpuUsage);
                    report.PeakMemoryUsage = relevantSnapshots.Max(s => s.MemoryUsage);
                    report.AverageProcessMemory = relevantSnapshots.Average(s => s.ProcessMemory);
                    report.PeakProcessMemory = relevantSnapshots.Max(s => s.ProcessMemory);
                    
                    // CPU distribution
                    report.CpuDistribution = new Dictionary<string, int>
                    {
                        ["0-25%"] = relevantSnapshots.Count(s => s.CpuUsage <= 25),
                        ["26-50%"] = relevantSnapshots.Count(s => s.CpuUsage > 25 && s.CpuUsage <= 50),
                        ["51-75%"] = relevantSnapshots.Count(s => s.CpuUsage > 50 && s.CpuUsage <= 75),
                        ["76-100%"] = relevantSnapshots.Count(s => s.CpuUsage > 75)
                    };
                    
                    // Memory distribution
                    report.MemoryDistribution = new Dictionary<string, int>
                    {
                        ["0-25%"] = relevantSnapshots.Count(s => s.MemoryUsage <= 25),
                        ["26-50%"] = relevantSnapshots.Count(s => s.MemoryUsage > 25 && s.MemoryUsage <= 50),
                        ["51-75%"] = relevantSnapshots.Count(s => s.MemoryUsage > 50 && s.MemoryUsage <= 75),
                        ["76-100%"] = relevantSnapshots.Count(s => s.MemoryUsage > 75)
                    };
                    
                    // Time series data
                    report.TimeSeriesData = relevantSnapshots
                        .Select(s => new TimeSeriesPoint
                        {
                            Timestamp = s.Timestamp,
                            CpuUsage = s.CpuUsage,
                            MemoryUsage = s.MemoryUsage,
                            ProcessMemory = s.ProcessMemory
                        })
                        .ToList();
                }
            }
            
            return report;
        }

        /// <summary>
        /// Exports performance data
        /// </summary>
        public async Task ExportDataAsync(string filePath, ExportFormat format = ExportFormat.Json)
        {
            var data = new
            {
                ExportDate = DateTime.Now,
                Metrics = CurrentMetrics,
                Statistics = Statistics,
                Snapshots = _snapshots.ToList()
            };
            
            switch (format)
            {
                case ExportFormat.Json:
                    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(filePath, json);
                    break;
                    
                case ExportFormat.Csv:
                    await ExportToCsvAsync(filePath);
                    break;
            }
        }

        /// <summary>
        /// Exports to CSV format
        /// </summary>
        private async Task ExportToCsvAsync(string filePath)
        {
            var lines = new List<string>
            {
                "Timestamp,CPU Usage,Memory Usage,Process Memory,Thread Count,Handle Count,GC Memory"
            };
            
            lock (_snapshots)
            {
                foreach (var snapshot in _snapshots)
                {
                    lines.Add($"{snapshot.Timestamp:yyyy-MM-dd HH:mm:ss},{snapshot.CpuUsage:F2},{snapshot.MemoryUsage:F2}," +
                             $"{snapshot.ProcessMemory},{snapshot.ThreadCount},{snapshot.HandleCount},{snapshot.GCMemory}");
                }
            }
            
            await File.WriteAllLinesAsync(filePath, lines);
        }

        /// <summary>
        /// Starts monitoring
        /// </summary>
        public void StartMonitoring()
        {
            if (!_isMonitoring)
            {
                _monitoringTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(5));
                _isMonitoring = true;
                _logger.Information("Performance monitoring started");
            }
        }

        /// <summary>
        /// Stops monitoring
        /// </summary>
        public void StopMonitoring()
        {
            if (_isMonitoring)
            {
                _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _isMonitoring = false;
                _logger.Information("Performance monitoring stopped");
            }
        }

        /// <summary>
        /// Clears performance data
        /// </summary>
        public void ClearData()
        {
            lock (_snapshots)
            {
                _snapshots.Clear();
            }
            
            CurrentMetrics = new PerformanceMetrics();
            Statistics = new PerformanceStatistics();
            _logger.Information("Performance data cleared");
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            StopMonitoring();
            _monitoringTimer?.Dispose();
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
            _logger?.Information("Performance monitor disposed");
        }
    }

    /// <summary>
    /// Performance snapshot
    /// </summary>
    public class PerformanceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public float CpuUsage { get; set; }
        public float MemoryUsage { get; set; }
        public long ProcessMemory { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public long GCMemory { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
    }

    /// <summary>
    /// Performance metrics
    /// </summary>
    public class PerformanceMetrics
    {
        public DateTime LastUpdate { get; set; }
        public float CurrentCpuUsage { get; set; }
        public float CurrentMemoryUsage { get; set; }
        public long CurrentProcessMemory { get; set; }
        public float AverageCpuUsage { get; set; }
        public float AverageMemoryUsage { get; set; }
        public float PeakCpuUsage { get; set; }
        public float PeakMemoryUsage { get; set; }
        public long PeakProcessMemory { get; set; }
    }

    /// <summary>
    /// Performance statistics
    /// </summary>
    public class PerformanceStatistics
    {
        public long TotalSamples { get; set; }
        public TimeSpan TotalMonitoringTime { get; set; }
        public int AlertsRaised { get; set; }
        public int CriticalAlerts { get; set; }
        public int WarningAlerts { get; set; }
        public DateTime? LastAlertTime { get; set; }
    }

    /// <summary>
    /// Performance alert
    /// </summary>
    public class PerformanceAlert
    {
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public float Value { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Alert types
    /// </summary>
    public enum AlertType
    {
        HighCpu,
        HighMemory,
        HighProcessMemory,
        LowMemory,
        HighThreadCount,
        HighGCPressure
    }

    /// <summary>
    /// Alert severity
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }

    /// <summary>
    /// Performance report
    /// </summary>
    public class PerformanceReport
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalSnapshots { get; set; }
        public float AverageCpuUsage { get; set; }
        public float AverageMemoryUsage { get; set; }
        public float PeakCpuUsage { get; set; }
        public float PeakMemoryUsage { get; set; }
        public float AverageProcessMemory { get; set; }
        public float PeakProcessMemory { get; set; }
        public Dictionary<string, int> CpuDistribution { get; set; } = new();
        public Dictionary<string, int> MemoryDistribution { get; set; } = new();
        public List<TimeSeriesPoint> TimeSeriesData { get; set; } = new();
    }

    /// <summary>
    /// Time series data point
    /// </summary>
    public class TimeSeriesPoint
    {
        public DateTime Timestamp { get; set; }
        public float CpuUsage { get; set; }
        public float MemoryUsage { get; set; }
        public float ProcessMemory { get; set; }
    }

    /// <summary>
    /// Export formats
    /// </summary>
    public enum ExportFormat
    {
        Json,
        Csv
    }
}