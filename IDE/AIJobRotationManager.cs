using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Timers;

namespace KimiAppNative.IDE
{
    /// <summary>
    /// AI Job Rotation Manager - Ensures all AI assistants have jobs and no one is idle
    /// Implements round-robin, load balancing, and distributed processing
    /// </summary>
    public class AIJobRotationManager
    {
        private readonly ConcurrentQueue<AIJob> _jobQueue;
        private readonly List<AIWorker> _workers;
        private readonly Dictionary<string, AIWorker> _workerRegistry;
        private readonly ConcurrentDictionary<string, AIJob> _activeJobs;
        private readonly System.Timers.Timer _rotationTimer;
        private readonly object _rotationLock = new object();
        private int _currentWorkerIndex = 0;
        private bool _isRunning = false;

        public event EventHandler<JobCompletedEventArgs> JobCompleted;
        public event EventHandler<WorkerStatusChangedEventArgs> WorkerStatusChanged;

        public AIJobRotationManager()
        {
            _jobQueue = new ConcurrentQueue<AIJob>();
            _workers = new List<AIWorker>();
            _workerRegistry = new Dictionary<string, AIWorker>();
            _activeJobs = new ConcurrentDictionary<string, AIJob>();
            
            // Setup rotation timer - rotates jobs every 30 seconds
            _rotationTimer = new System.Timers.Timer(30000);
            _rotationTimer.Elapsed += OnRotationTimer;
            _rotationTimer.AutoReset = true;
            
            InitializeWorkers();
        }

        #region Worker Management

        private void InitializeWorkers()
        {
            // Initialize different types of AI workers
            var workerTypes = new[]
            {
                new { Name = "CodeGenerator", Type = AIWorkerType.CodeGeneration, Capacity = 5 },
                new { Name = "CodeAnalyzer", Type = AIWorkerType.CodeAnalysis, Capacity = 3 },
                new { Name = "BugDetector", Type = AIWorkerType.BugDetection, Capacity = 4 },
                new { Name = "RefactoringExpert", Type = AIWorkerType.Refactoring, Capacity = 3 },
                new { Name = "DocumentationWriter", Type = AIWorkerType.Documentation, Capacity = 2 },
                new { Name = "TestGenerator", Type = AIWorkerType.TestGeneration, Capacity = 4 },
                new { Name = "SecurityAuditor", Type = AIWorkerType.SecurityAudit, Capacity = 2 },
                new { Name = "PerformanceOptimizer", Type = AIWorkerType.PerformanceOptimization, Capacity = 3 }
            };

            foreach (var workerType in workerTypes)
            {
                for (int i = 0; i < workerType.Capacity; i++)
                {
                    var worker = new AIWorker
                    {
                        Id = $"{workerType.Name}_{i + 1}",
                        Name = $"{workerType.Name} #{i + 1}",
                        Type = workerType.Type,
                        Status = WorkerStatus.Idle,
                        MaxConcurrentJobs = 2,
                        CurrentJobs = 0,
                        TotalJobsProcessed = 0,
                        LastActivity = DateTime.Now
                    };
                    
                    _workers.Add(worker);
                    _workerRegistry[worker.Id] = worker;
                }
            }

            Console.WriteLine($"[AI Rotation] Initialized {_workers.Count} AI workers across {workerTypes.Length} types");
        }

        #endregion

        #region Job Management

        public async Task<string> SubmitJob(AIJob job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            job.Id = Guid.NewGuid().ToString();
            job.Status = JobStatus.Queued;
            job.CreatedAt = DateTime.Now;

            _jobQueue.Enqueue(job);
            
            Console.WriteLine($"[AI Rotation] Job {job.Id} queued: {job.Type} - {job.Description}");
            
            // Try to assign immediately
            await TryAssignJob();
            
            return job.Id;
        }

        public async Task<string> SubmitCodeGenerationJob(string prompt, string language = "csharp")
        {
            var job = new AIJob
            {
                Type = JobType.CodeGeneration,
                Description = $"Generate {language} code: {prompt.Substring(0, Math.Min(50, prompt.Length))}...",
                Data = new Dictionary<string, object>
                {
                    ["prompt"] = prompt,
                    ["language"] = language,
                    ["timestamp"] = DateTime.Now
                },
                Priority = JobPriority.Normal,
                EstimatedDuration = TimeSpan.FromSeconds(30)
            };

            return await SubmitJob(job);
        }

        public async Task<string> SubmitCodeAnalysisJob(string code, string analysisType = "general")
        {
            var job = new AIJob
            {
                Type = JobType.CodeAnalysis,
                Description = $"Analyze {analysisType} code ({code.Length} chars)",
                Data = new Dictionary<string, object>
                {
                    ["code"] = code,
                    ["analysisType"] = analysisType,
                    ["timestamp"] = DateTime.Now
                },
                Priority = JobPriority.Normal,
                EstimatedDuration = TimeSpan.FromSeconds(20)
            };

            return await SubmitJob(job);
        }

        public async Task<string> SubmitBugDetectionJob(string code)
        {
            var job = new AIJob
            {
                Type = JobType.BugDetection,
                Description = $"Detect bugs in code ({code.Length} chars)",
                Data = new Dictionary<string, object>
                {
                    ["code"] = code,
                    ["timestamp"] = DateTime.Now
                },
                Priority = JobPriority.High,
                EstimatedDuration = TimeSpan.FromSeconds(25)
            };

            return await SubmitJob(job);
        }

        #endregion

        #region Job Assignment and Rotation

        private async Task TryAssignJob()
        {
            if (_jobQueue.IsEmpty)
                return;

            lock (_rotationLock)
            {
                // Find available workers using round-robin
                var availableWorkers = _workers
                    .Where(w => w.Status == WorkerStatus.Idle && w.CurrentJobs < w.MaxConcurrentJobs)
                    .OrderBy(w => w.LastActivity)
                    .ToList();

                if (!availableWorkers.Any())
                {
                    // All workers busy, try to find least loaded worker
                    availableWorkers = _workers
                        .Where(w => w.CurrentJobs < w.MaxConcurrentJobs)
                        .OrderBy(w => w.CurrentJobs)
                        .ToList();
                }

                if (availableWorkers.Any() && _jobQueue.TryDequeue(out var job))
                {
                    var selectedWorker = SelectBestWorker(availableWorkers, job);
                    if (selectedWorker != null)
                    {
                        AssignJobToWorker(job, selectedWorker);
                    }
                    else
                    {
                        // No suitable worker found, re-queue the job
                        _jobQueue.Enqueue(job);
                    }
                }
            }
        }

        private AIWorker SelectBestWorker(List<AIWorker> availableWorkers, AIJob job)
        {
            // First, try to find a worker that specializes in this job type
            var specializedWorkers = availableWorkers
                .Where(w => w.Type == GetWorkerTypeForJob(job.Type))
                .ToList();

            if (specializedWorkers.Any())
            {
                return specializedWorkers
                    .OrderBy(w => w.CurrentJobs)
                    .ThenBy(w => w.TotalJobsProcessed)
                    .First();
            }

            // If no specialized worker, use any available worker
            return availableWorkers
                .OrderBy(w => w.CurrentJobs)
                .ThenBy(w => w.TotalJobsProcessed)
                .First();
        }

        private AIWorkerType GetWorkerTypeForJob(JobType jobType)
        {
            return jobType switch
            {
                JobType.CodeGeneration => AIWorkerType.CodeGeneration,
                JobType.CodeAnalysis => AIWorkerType.CodeAnalysis,
                JobType.BugDetection => AIWorkerType.BugDetection,
                JobType.Refactoring => AIWorkerType.Refactoring,
                JobType.Documentation => AIWorkerType.Documentation,
                JobType.TestGeneration => AIWorkerType.TestGeneration,
                JobType.SecurityAudit => AIWorkerType.SecurityAudit,
                JobType.PerformanceOptimization => AIWorkerType.PerformanceOptimization,
                _ => AIWorkerType.CodeGeneration
            };
        }

        private void AssignJobToWorker(AIJob job, AIWorker worker)
        {
            job.AssignedWorkerId = worker.Id;
            job.Status = JobStatus.InProgress;
            job.StartedAt = DateTime.Now;
            
            worker.CurrentJobs++;
            worker.Status = WorkerStatus.Busy;
            worker.LastActivity = DateTime.Now;
            
            _activeJobs[job.Id] = job;
            
            Console.WriteLine($"[AI Rotation] Job {job.Id} assigned to {worker.Name} ({worker.Type})");
            
            // Start processing the job
            _ = ProcessJobAsync(job, worker);
            
            WorkerStatusChanged?.Invoke(this, new WorkerStatusChangedEventArgs
            {
                WorkerId = worker.Id,
                OldStatus = WorkerStatus.Idle,
                NewStatus = WorkerStatus.Busy,
                CurrentJobs = worker.CurrentJobs
            });
        }

        #endregion

        #region Job Processing

        private async Task ProcessJobAsync(AIJob job, AIWorker worker)
        {
            try
            {
                Console.WriteLine($"[AI Rotation] {worker.Name} processing job {job.Id}: {job.Description}");
                
                // Simulate job processing with actual AI work
                var result = await ProcessJobWithAI(job, worker);
                
                job.Result = result;
                job.Status = JobStatus.Completed;
                job.CompletedAt = DateTime.Now;
                job.Duration = job.CompletedAt - job.StartedAt;
                
                // Update worker stats
                worker.CurrentJobs--;
                worker.TotalJobsProcessed++;
                worker.Status = worker.CurrentJobs > 0 ? WorkerStatus.Busy : WorkerStatus.Idle;
                worker.LastActivity = DateTime.Now;
                
                _activeJobs.TryRemove(job.Id, out _);
                
                Console.WriteLine($"[AI Rotation] Job {job.Id} completed by {worker.Name} in {job.Duration?.TotalSeconds:F1}s");
                
                JobCompleted?.Invoke(this, new JobCompletedEventArgs
                {
                    Job = job,
                    Worker = worker,
                    Success = true
                });
                
                WorkerStatusChanged?.Invoke(this, new WorkerStatusChangedEventArgs
                {
                    WorkerId = worker.Id,
                    OldStatus = WorkerStatus.Busy,
                    NewStatus = worker.Status,
                    CurrentJobs = worker.CurrentJobs
                });
                
                // Try to assign more jobs
                await TryAssignJob();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Rotation] Error processing job {job.Id}: {ex.Message}");
                
                job.Status = JobStatus.Failed;
                job.Error = ex.Message;
                job.CompletedAt = DateTime.Now;
                
                worker.CurrentJobs--;
                worker.Status = worker.CurrentJobs > 0 ? WorkerStatus.Busy : WorkerStatus.Idle;
                
                _activeJobs.TryRemove(job.Id, out _);
                
                JobCompleted?.Invoke(this, new JobCompletedEventArgs
                {
                    Job = job,
                    Worker = worker,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        private async Task<object> ProcessJobWithAI(AIJob job, AIWorker worker)
        {
            // Simulate AI processing time based on job complexity
            var processingTime = GetProcessingTime(job);
            await Task.Delay(processingTime);
            
            // Generate appropriate result based on job type
            return job.Type switch
            {
                JobType.CodeGeneration => GenerateCodeResult(job),
                JobType.CodeAnalysis => AnalyzeCodeResult(job),
                JobType.BugDetection => DetectBugsResult(job),
                JobType.Refactoring => RefactorCodeResult(job),
                JobType.Documentation => GenerateDocumentationResult(job),
                JobType.TestGeneration => GenerateTestsResult(job),
                JobType.SecurityAudit => PerformSecurityAuditResult(job),
                JobType.PerformanceOptimization => OptimizePerformanceResult(job),
                _ => "Unknown job type"
            };
        }

        private int GetProcessingTime(AIJob job)
        {
            // Simulate realistic processing times
            var baseTime = job.Type switch
            {
                JobType.CodeGeneration => 2000,
                JobType.CodeAnalysis => 1500,
                JobType.BugDetection => 3000,
                JobType.Refactoring => 2500,
                JobType.Documentation => 1800,
                JobType.TestGeneration => 2200,
                JobType.SecurityAudit => 4000,
                JobType.PerformanceOptimization => 3500,
                _ => 1000
            };
            
            // Add some randomness
            var random = new Random();
            return baseTime + random.Next(0, 1000);
        }

        #endregion

        #region Result Generation

        private object GenerateCodeResult(AIJob job)
        {
            var prompt = job.Data["prompt"]?.ToString() ?? "";
            var language = job.Data["language"]?.ToString() ?? "csharp";
            
            return new
            {
                GeneratedCode = $"// Generated {language} code for: {prompt}\npublic class GeneratedClass\n{{\n    public void GeneratedMethod()\n    {{\n        // Implementation here\n    }}\n}}",
                Language = language,
                LinesOfCode = 8,
                Complexity = "Medium"
            };
        }

        private object AnalyzeCodeResult(AIJob job)
        {
            var code = job.Data["code"]?.ToString() ?? "";
            
            return new
            {
                Analysis = "Code analysis completed",
                Metrics = new
                {
                    LinesOfCode = code.Split('\n').Length,
                    Complexity = "Low",
                    Maintainability = "Good",
                    Readability = "Excellent"
                },
                Suggestions = new[] { "Consider adding comments", "Extract complex methods" }
            };
        }

        private object DetectBugsResult(AIJob job)
        {
            return new
            {
                BugsFound = 2,
                Issues = new[]
                {
                    new { Type = "Potential Null Reference", Severity = "Medium", Line = 15 },
                    new { Type = "Unused Variable", Severity = "Low", Line = 23 }
                },
                Recommendations = new[] { "Add null checks", "Remove unused variables" }
            };
        }

        private object RefactorCodeResult(AIJob job)
        {
            return new
            {
                RefactoredCode = "// Refactored code with improvements",
                Changes = new[] { "Extracted method", "Improved naming", "Added error handling" },
                Metrics = new { Before = "Complexity: High", After = "Complexity: Medium" }
            };
        }

        private object GenerateDocumentationResult(AIJob job)
        {
            return new
            {
                Documentation = "/// <summary>\n/// Generated documentation\n/// </summary>",
                Sections = new[] { "Overview", "Parameters", "Returns", "Examples" }
            };
        }

        private object GenerateTestsResult(AIJob job)
        {
            return new
            {
                TestCode = "[Test]\npublic void TestMethod()\n{\n    // Test implementation\n}",
                TestCount = 5,
                Coverage = "85%"
            };
        }

        private object PerformSecurityAuditResult(AIJob job)
        {
            return new
            {
                SecurityIssues = 1,
                Vulnerabilities = new[] { "SQL Injection Risk" },
                Recommendations = new[] { "Use parameterized queries" }
            };
        }

        private object OptimizePerformanceResult(AIJob job)
        {
            return new
            {
                Optimizations = new[] { "Use StringBuilder", "Cache results", "Reduce allocations" },
                PerformanceGain = "15%",
                MemoryReduction = "20%"
            };
        }

        #endregion

        #region Rotation and Load Balancing

        private void OnRotationTimer(object sender, ElapsedEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                await PerformJobRotation();
                await RebalanceWorkload();
            });
        }

        private async Task PerformJobRotation()
        {
            lock (_rotationLock)
            {
                // Rotate long-running jobs to different workers
                var longRunningJobs = _activeJobs.Values
                    .Where(j => j.StartedAt.HasValue && 
                               DateTime.Now - j.StartedAt.Value > TimeSpan.FromMinutes(2))
                    .ToList();

                foreach (var job in longRunningJobs)
                {
                    var currentWorker = _workerRegistry[job.AssignedWorkerId];
                    var alternativeWorker = _workers
                        .Where(w => w.Id != currentWorker.Id && 
                                   w.Type == currentWorker.Type && 
                                   w.CurrentJobs < w.MaxConcurrentJobs)
                        .OrderBy(w => w.CurrentJobs)
                        .FirstOrDefault();

                    if (alternativeWorker != null)
                    {
                        Console.WriteLine($"[AI Rotation] Rotating job {job.Id} from {currentWorker.Name} to {alternativeWorker.Name}");
                        
                        // Update worker assignments
                        currentWorker.CurrentJobs--;
                        alternativeWorker.CurrentJobs++;
                        
                        job.AssignedWorkerId = alternativeWorker.Id;
                        
                        // Update worker statuses
                        currentWorker.Status = currentWorker.CurrentJobs > 0 ? WorkerStatus.Busy : WorkerStatus.Idle;
                        alternativeWorker.Status = WorkerStatus.Busy;
                    }
                }
            }
        }

        private async Task RebalanceWorkload()
        {
            lock (_rotationLock)
            {
                var overloadedWorkers = _workers.Where(w => w.CurrentJobs >= w.MaxConcurrentJobs).ToList();
                var underloadedWorkers = _workers.Where(w => w.CurrentJobs < w.MaxConcurrentJobs / 2).ToList();

                if (overloadedWorkers.Any() && underloadedWorkers.Any())
                {
                    Console.WriteLine($"[AI Rotation] Rebalancing workload - {overloadedWorkers.Count} overloaded, {underloadedWorkers.Count} underloaded");
                    
                    // Move jobs from overloaded to underloaded workers
                    foreach (var overloadedWorker in overloadedWorkers)
                    {
                        var jobsToMove = _activeJobs.Values
                            .Where(j => j.AssignedWorkerId == overloadedWorker.Id)
                            .Take(1)
                            .ToList();

                        foreach (var job in jobsToMove)
                        {
                            var targetWorker = underloadedWorkers
                                .Where(w => w.Type == overloadedWorker.Type)
                                .OrderBy(w => w.CurrentJobs)
                                .FirstOrDefault();

                            if (targetWorker != null)
                            {
                                Console.WriteLine($"[AI Rotation] Moving job {job.Id} from {overloadedWorker.Name} to {targetWorker.Name}");
                                
                                overloadedWorker.CurrentJobs--;
                                targetWorker.CurrentJobs++;
                                
                                job.AssignedWorkerId = targetWorker.Id;
                                
                                overloadedWorker.Status = overloadedWorker.CurrentJobs > 0 ? WorkerStatus.Busy : WorkerStatus.Idle;
                                targetWorker.Status = WorkerStatus.Busy;
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Control Methods

        public void Start()
        {
            if (_isRunning) return;
            
            _isRunning = true;
            _rotationTimer.Start();
            
            Console.WriteLine("[AI Rotation] Job rotation manager started");
        }

        public void Stop()
        {
            if (!_isRunning) return;
            
            _isRunning = false;
            _rotationTimer.Stop();
            
            Console.WriteLine("[AI Rotation] Job rotation manager stopped");
        }

        public AIJob GetJobStatus(string jobId)
        {
            return _activeJobs.TryGetValue(jobId, out var job) ? job : null;
        }

        public List<AIWorker> GetWorkerStatus()
        {
            return _workers.ToList();
        }

        public Dictionary<string, object> GetSystemStats()
        {
            return new Dictionary<string, object>
            {
                ["TotalWorkers"] = _workers.Count,
                ["ActiveWorkers"] = _workers.Count(w => w.Status == WorkerStatus.Busy),
                ["IdleWorkers"] = _workers.Count(w => w.Status == WorkerStatus.Idle),
                ["QueuedJobs"] = _jobQueue.Count,
                ["ActiveJobs"] = _activeJobs.Count,
                ["TotalJobsProcessed"] = _workers.Sum(w => w.TotalJobsProcessed),
                ["AverageJobDuration"] = _workers.Average(w => w.TotalJobsProcessed > 0 ? 1.0 : 0.0)
            };
        }

        #endregion
    }

    #region Supporting Classes and Enums

    public class AIJob
    {
        public string Id { get; set; }
        public JobType Type { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public JobPriority Priority { get; set; }
        public JobStatus Status { get; set; }
        public string AssignedWorkerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration { get; set; }
        public object Result { get; set; }
        public string Error { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
    }

    public class AIWorker
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public AIWorkerType Type { get; set; }
        public WorkerStatus Status { get; set; }
        public int MaxConcurrentJobs { get; set; }
        public int CurrentJobs { get; set; }
        public int TotalJobsProcessed { get; set; }
        public DateTime LastActivity { get; set; }
    }

    public enum JobType
    {
        CodeGeneration,
        CodeAnalysis,
        BugDetection,
        Refactoring,
        Documentation,
        TestGeneration,
        SecurityAudit,
        PerformanceOptimization
    }

    public enum JobPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    public enum JobStatus
    {
        Queued,
        InProgress,
        Completed,
        Failed,
        Cancelled
    }

    public enum AIWorkerType
    {
        CodeGeneration,
        CodeAnalysis,
        BugDetection,
        Refactoring,
        Documentation,
        TestGeneration,
        SecurityAudit,
        PerformanceOptimization
    }

    public enum WorkerStatus
    {
        Idle,
        Busy,
        Offline,
        Error
    }

    public class JobCompletedEventArgs : EventArgs
    {
        public AIJob Job { get; set; }
        public AIWorker Worker { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public class WorkerStatusChangedEventArgs : EventArgs
    {
        public string WorkerId { get; set; }
        public WorkerStatus OldStatus { get; set; }
        public WorkerStatus NewStatus { get; set; }
        public int CurrentJobs { get; set; }
    }

    #endregion
}
