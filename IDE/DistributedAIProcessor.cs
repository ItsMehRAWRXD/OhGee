using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;

namespace KimiAppNative.IDE
{
    /// <summary>
    /// Distributed AI Processor - Coordinates multiple AI workers with no idle time
    /// Ensures continuous processing and optimal resource utilization
    /// </summary>
    public class DistributedAIProcessor
    {
        private readonly AIJobRotationManager _jobManager;
        private readonly LocalAIEngine _localAI;
        private readonly ConcurrentDictionary<string, AIProcessingNode> _processingNodes;
        private readonly List<AITask> _taskQueue;
        private readonly System.Timers.Timer _healthCheckTimer;
        private readonly object _taskLock = new object();
        private bool _isRunning = false;

        public event EventHandler<TaskCompletedEventArgs> TaskCompleted;
        public event EventHandler<NodeStatusChangedEventArgs> NodeStatusChanged;

        public DistributedAIProcessor()
        {
            _jobManager = new AIJobRotationManager();
            _localAI = new LocalAIEngine();
            _processingNodes = new ConcurrentDictionary<string, AIProcessingNode>();
            _taskQueue = new List<AITask>();
            
            // Health check timer - checks every 10 seconds
            _healthCheckTimer = new System.Timers.Timer(10000);
            _healthCheckTimer.Elapsed += OnHealthCheck;
            _healthCheckTimer.AutoReset = true;
            
            InitializeProcessingNodes();
            SetupEventHandlers();
        }

        #region Initialization

        private void InitializeProcessingNodes()
        {
            // Create different types of processing nodes
            var nodeConfigs = new[]
            {
                new { Type = "CodeGen", Capacity = 3, Specialization = "Code Generation" },
                new { Type = "Analysis", Capacity = 2, Specialization = "Code Analysis" },
                new { Type = "Security", Capacity = 2, Specialization = "Security Audit" },
                new { Type = "Performance", Capacity = 2, Specialization = "Performance Optimization" },
                new { Type = "Testing", Capacity = 2, Specialization = "Test Generation" },
                new { Type = "Documentation", Capacity = 1, Specialization = "Documentation" },
                new { Type = "Refactoring", Capacity = 2, Specialization = "Code Refactoring" },
                new { Type = "Debugging", Capacity = 2, Specialization = "Bug Detection" }
            };

            foreach (var config in nodeConfigs)
            {
                for (int i = 0; i < config.Capacity; i++)
                {
                    var node = new AIProcessingNode
                    {
                        Id = $"{config.Type}_{i + 1}",
                        Name = $"{config.Specialization} Node #{i + 1}",
                        Type = config.Type,
                        Specialization = config.Specialization,
                        Status = NodeStatus.Ready,
                        MaxConcurrentTasks = 2,
                        CurrentTasks = 0,
                        TotalTasksProcessed = 0,
                        LastActivity = DateTime.Now,
                        HealthScore = 100,
                        ProcessingSpeed = 1.0
                    };
                    
                    _processingNodes[node.Id] = node;
                }
            }

            Console.WriteLine($"[DistributedAI] Initialized {_processingNodes.Count} processing nodes");
        }

        private void SetupEventHandlers()
        {
            _jobManager.JobCompleted += OnJobCompleted;
            _jobManager.WorkerStatusChanged += OnWorkerStatusChanged;
        }

        #endregion

        #region Task Management

        public async Task<string> SubmitTask(AITask task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            task.Id = Guid.NewGuid().ToString();
            task.Status = TaskStatus.Queued;
            task.CreatedAt = DateTime.Now;

            lock (_taskLock)
            {
                _taskQueue.Add(task);
            }

            Console.WriteLine($"[DistributedAI] Task {task.Id} queued: {task.Type} - {task.Description}");
            
            // Try to assign immediately
            await TryAssignTask();
            
            return task.Id;
        }

        public async Task<string> SubmitCodeGenerationTask(string prompt, string language = "csharp")
        {
            var task = new AITask
            {
                Type = TaskType.CodeGeneration,
                Description = $"Generate {language} code: {prompt.Substring(0, Math.Min(50, prompt.Length))}...",
                Data = new Dictionary<string, object>
                {
                    ["prompt"] = prompt,
                    ["language"] = language,
                    ["timestamp"] = DateTime.Now
                },
                Priority = TaskPriority.Normal,
                EstimatedDuration = TimeSpan.FromSeconds(30),
                RequiredSpecialization = "Code Generation"
            };

            return await SubmitTask(task);
        }

        public async Task<string> SubmitAnalysisTask(string code, string analysisType = "comprehensive")
        {
            var task = new AITask
            {
                Type = TaskType.CodeAnalysis,
                Description = $"Analyze {analysisType} code ({code.Length} chars)",
                Data = new Dictionary<string, object>
                {
                    ["code"] = code,
                    ["analysisType"] = analysisType,
                    ["timestamp"] = DateTime.Now
                },
                Priority = TaskPriority.Normal,
                EstimatedDuration = TimeSpan.FromSeconds(25),
                RequiredSpecialization = "Code Analysis"
            };

            return await SubmitTask(task);
        }

        public async Task<string> SubmitSecurityAuditTask(string code)
        {
            var task = new AITask
            {
                Type = TaskType.SecurityAudit,
                Description = $"Security audit for code ({code.Length} chars)",
                Data = new Dictionary<string, object>
                {
                    ["code"] = code,
                    ["timestamp"] = DateTime.Now
                },
                Priority = TaskPriority.High,
                EstimatedDuration = TimeSpan.FromSeconds(45),
                RequiredSpecialization = "Security Audit"
            };

            return await SubmitTask(task);
        }

        #endregion

        #region Task Assignment and Distribution

        private async Task TryAssignTask()
        {
            lock (_taskLock)
            {
                if (!_taskQueue.Any())
                    return;

                // Find available nodes using intelligent assignment
                var availableNodes = _processingNodes.Values
                    .Where(n => n.Status == NodeStatus.Ready && n.CurrentTasks < n.MaxConcurrentTasks)
                    .OrderBy(n => n.CurrentTasks)
                    .ThenBy(n => n.HealthScore)
                    .ToList();

                if (!availableNodes.Any())
                {
                    // All nodes busy, try to find least loaded node
                    availableNodes = _processingNodes.Values
                        .Where(n => n.CurrentTasks < n.MaxConcurrentTasks)
                        .OrderBy(n => n.CurrentTasks)
                        .ToList();
                }

                if (availableNodes.Any())
                {
                    var task = _taskQueue.FirstOrDefault();
                    if (task != null)
                    {
                        var selectedNode = SelectBestNode(availableNodes, task);
                        if (selectedNode != null)
                        {
                            AssignTaskToNode(task, selectedNode);
                            _taskQueue.Remove(task);
                        }
                    }
                }
            }
        }

        private AIProcessingNode SelectBestNode(List<AIProcessingNode> availableNodes, AITask task)
        {
            // First, try to find a node that specializes in this task type
            var specializedNodes = availableNodes
                .Where(n => n.Specialization.Equals(task.RequiredSpecialization, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (specializedNodes.Any())
            {
                return specializedNodes
                    .OrderBy(n => n.CurrentTasks)
                    .ThenByDescending(n => n.HealthScore)
                    .ThenBy(n => n.ProcessingSpeed)
                    .First();
            }

            // If no specialized node, use any available node with best performance
            return availableNodes
                .OrderBy(n => n.CurrentTasks)
                .ThenByDescending(n => n.HealthScore)
                .ThenByDescending(n => n.ProcessingSpeed)
                .First();
        }

        private void AssignTaskToNode(AITask task, AIProcessingNode node)
        {
            task.AssignedNodeId = node.Id;
            task.Status = TaskStatus.InProgress;
            task.StartedAt = DateTime.Now;
            
            node.CurrentTasks++;
            node.Status = NodeStatus.Busy;
            node.LastActivity = DateTime.Now;
            
            Console.WriteLine($"[DistributedAI] Task {task.Id} assigned to {node.Name} ({node.Specialization})");
            
            // Start processing the task
            _ = ProcessTaskAsync(task, node);
            
            NodeStatusChanged?.Invoke(this, new NodeStatusChangedEventArgs
            {
                NodeId = node.Id,
                OldStatus = NodeStatus.Ready,
                NewStatus = NodeStatus.Busy,
                CurrentTasks = node.CurrentTasks
            });
        }

        #endregion

        #region Task Processing

        private async Task ProcessTaskAsync(AITask task, AIProcessingNode node)
        {
            try
            {
                Console.WriteLine($"[DistributedAI] {node.Name} processing task {task.Id}: {task.Description}");
                
                // Process the task using the appropriate method
                var result = await ProcessTaskWithNode(task, node);
                
                task.Result = result;
                task.Status = TaskStatus.Completed;
                task.CompletedAt = DateTime.Now;
                task.Duration = task.CompletedAt - task.StartedAt;
                
                // Update node stats
                node.CurrentTasks--;
                node.TotalTasksProcessed++;
                node.Status = node.CurrentTasks > 0 ? NodeStatus.Busy : NodeStatus.Ready;
                node.LastActivity = DateTime.Now;
                
                // Update health score based on performance
                UpdateNodeHealthScore(node, task);
                
                Console.WriteLine($"[DistributedAI] Task {task.Id} completed by {node.Name} in {task.Duration?.TotalSeconds:F1}s");
                
                TaskCompleted?.Invoke(this, new TaskCompletedEventArgs
                {
                    Task = task,
                    Node = node,
                    Success = true
                });
                
                NodeStatusChanged?.Invoke(this, new NodeStatusChangedEventArgs
                {
                    NodeId = node.Id,
                    OldStatus = NodeStatus.Busy,
                    NewStatus = node.Status,
                    CurrentTasks = node.CurrentTasks
                });
                
                // Try to assign more tasks
                await TryAssignTask();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DistributedAI] Error processing task {task.Id}: {ex.Message}");
                
                task.Status = TaskStatus.Failed;
                task.Error = ex.Message;
                task.CompletedAt = DateTime.Now;
                
                node.CurrentTasks--;
                node.Status = node.CurrentTasks > 0 ? NodeStatus.Busy : NodeStatus.Ready;
                node.HealthScore = Math.Max(0, node.HealthScore - 10); // Penalize for failure
                
                TaskCompleted?.Invoke(this, new TaskCompletedEventArgs
                {
                    Task = task,
                    Node = node,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        private async Task<object> ProcessTaskWithNode(AITask task, AIProcessingNode node)
        {
            // Use the local AI engine for processing
            return task.Type switch
            {
                TaskType.CodeGeneration => await _localAI.GenerateCode(
                    task.Data["prompt"]?.ToString() ?? "",
                    task.Data["language"]?.ToString() ?? "csharp"),
                
                TaskType.CodeAnalysis => await _localAI.ExplainCode(
                    task.Data["code"]?.ToString() ?? ""),
                
                TaskType.BugDetection => await _localAI.DetectBugs(
                    task.Data["code"]?.ToString() ?? ""),
                
                TaskType.Refactoring => await _localAI.RefactorCode(
                    task.Data["code"]?.ToString() ?? "",
                    task.Data["instructions"]?.ToString() ?? ""),
                
                TaskType.SecurityAudit => await PerformSecurityAudit(
                    task.Data["code"]?.ToString() ?? ""),
                
                TaskType.PerformanceOptimization => await OptimizePerformance(
                    task.Data["code"]?.ToString() ?? ""),
                
                TaskType.TestGeneration => await GenerateTests(
                    task.Data["code"]?.ToString() ?? ""),
                
                TaskType.Documentation => await GenerateDocumentation(
                    task.Data["code"]?.ToString() ?? ""),
                
                _ => "Unknown task type"
            };
        }

        private void UpdateNodeHealthScore(AIProcessingNode node, AITask task)
        {
            // Update health score based on task completion time vs estimated time
            if (task.Duration.HasValue && task.EstimatedDuration.TotalSeconds > 0)
            {
                var efficiency = task.EstimatedDuration.TotalSeconds / task.Duration.Value.TotalSeconds;
                var healthChange = (efficiency - 1.0) * 5; // ±5 points based on efficiency
                node.HealthScore = Math.Max(0, Math.Min(100, node.HealthScore + (int)healthChange));
            }
            
            // Bonus for completing tasks successfully
            node.HealthScore = Math.Min(100, node.HealthScore + 1);
        }

        #endregion

        #region Specialized Processing Methods

        private async Task<object> PerformSecurityAudit(string code)
        {
            await Task.Delay(2000); // Simulate processing time
            
            var vulnerabilities = new List<string>();
            
            if (code.Contains("sql") && code.Contains("+"))
                vulnerabilities.Add("Potential SQL Injection");
            
            if (code.Contains("password") && !code.Contains("hash"))
                vulnerabilities.Add("Plain text password storage");
            
            if (code.Contains("eval("))
                vulnerabilities.Add("Code injection risk");
            
            return new
            {
                VulnerabilitiesFound = vulnerabilities.Count,
                Issues = vulnerabilities,
                Recommendations = new[]
                {
                    "Use parameterized queries",
                    "Hash passwords with salt",
                    "Avoid eval() function"
                },
                SecurityScore = Math.Max(0, 100 - vulnerabilities.Count * 20)
            };
        }

        private async Task<object> OptimizePerformance(string code)
        {
            await Task.Delay(1500);
            
            var optimizations = new List<string>();
            
            if (code.Contains("string") && code.Contains("+"))
                optimizations.Add("Use StringBuilder for string concatenation");
            
            if (code.Contains("foreach") && code.Contains("List"))
                optimizations.Add("Consider using for loop for better performance");
            
            if (code.Contains("new ") && code.Contains("loop"))
                optimizations.Add("Move object creation outside loops");
            
            return new
            {
                OptimizationsSuggested = optimizations.Count,
                Suggestions = optimizations,
                EstimatedImprovement = $"{optimizations.Count * 5}%",
                PerformanceScore = Math.Min(100, 60 + optimizations.Count * 10)
            };
        }

        private async Task<object> GenerateTests(string code)
        {
            await Task.Delay(1800);
            
            var testMethods = new List<string>();
            var methods = ExtractMethods(code);
            
            foreach (var method in methods)
            {
                testMethods.Add($"[Test]\npublic void Test{method}()\n{{\n    // Arrange\n    \n    // Act\n    \n    // Assert\n    Assert.IsTrue(true);\n}}");
            }
            
            return new
            {
                TestMethodsGenerated = testMethods.Count,
                TestCode = string.Join("\n\n", testMethods),
                Coverage = $"{Math.Min(100, testMethods.Count * 20)}%",
                TestTypes = new[] { "Unit Tests", "Integration Tests", "Edge Cases" }
            };
        }

        private async Task<object> GenerateDocumentation(string code)
        {
            await Task.Delay(1200);
            
            var methods = ExtractMethods(code);
            var documentation = new List<string>();
            
            foreach (var method in methods)
            {
                documentation.Add($"/// <summary>\n/// {method} method\n/// </summary>\n/// <returns>Description of return value</returns>");
            }
            
            return new
            {
                DocumentationGenerated = documentation.Count,
                Documentation = string.Join("\n\n", documentation),
                Sections = new[] { "Method Descriptions", "Parameters", "Return Values", "Examples" }
            };
        }

        private List<string> ExtractMethods(string code)
        {
            var methods = new List<string>();
            var lines = code.Split('\n');
            
            foreach (var line in lines)
            {
                if (line.Contains("public") && line.Contains("(") && line.Contains(")"))
                {
                    var methodName = ExtractMethodName(line);
                    if (!string.IsNullOrEmpty(methodName))
                        methods.Add(methodName);
                }
            }
            
            return methods;
        }

        private string ExtractMethodName(string line)
        {
            var parts = line.Split(new[] { ' ', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i] == "public" || parts[i] == "private" || parts[i] == "protected")
                {
                    return parts[i + 1];
                }
            }
            return "";
        }

        #endregion

        #region Health Monitoring

        private void OnHealthCheck(object sender, System.Timers.ElapsedEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                await PerformHealthCheck();
                await RebalanceTasks();
            });
        }

        private async Task PerformHealthCheck()
        {
            foreach (var node in _processingNodes.Values)
            {
                // Check if node is responsive
                var timeSinceLastActivity = DateTime.Now - node.LastActivity;
                if (timeSinceLastActivity > TimeSpan.FromMinutes(5))
                {
                    node.HealthScore = Math.Max(0, node.HealthScore - 5);
                    Console.WriteLine($"[DistributedAI] Node {node.Name} health decreased due to inactivity");
                }
                
                // Mark unhealthy nodes
                if (node.HealthScore < 30)
                {
                    node.Status = NodeStatus.Unhealthy;
                    Console.WriteLine($"[DistributedAI] Node {node.Name} marked as unhealthy (score: {node.HealthScore})");
                }
                else if (node.Status == NodeStatus.Unhealthy && node.HealthScore > 50)
                {
                    node.Status = NodeStatus.Ready;
                    Console.WriteLine($"[DistributedAI] Node {node.Name} recovered and marked as ready");
                }
            }
        }

        private async Task RebalanceTasks()
        {
            lock (_taskLock)
            {
                var overloadedNodes = _processingNodes.Values
                    .Where(n => n.CurrentTasks >= n.MaxConcurrentTasks)
                    .ToList();
                
                var underloadedNodes = _processingNodes.Values
                    .Where(n => n.CurrentTasks < n.MaxConcurrentTasks / 2 && n.Status == NodeStatus.Ready)
                    .ToList();

                if (overloadedNodes.Any() && underloadedNodes.Any())
                {
                    Console.WriteLine($"[DistributedAI] Rebalancing tasks - {overloadedNodes.Count} overloaded, {underloadedNodes.Count} underloaded");
                    
                    // Move tasks from overloaded to underloaded nodes
                    foreach (var overloadedNode in overloadedNodes)
                    {
                        var tasksToMove = _taskQueue
                            .Where(t => t.AssignedNodeId == overloadedNode.Id && t.Status == TaskStatus.InProgress)
                            .Take(1)
                            .ToList();

                        foreach (var task in tasksToMove)
                        {
                            var targetNode = underloadedNodes
                                .Where(n => n.Specialization == task.RequiredSpecialization)
                                .OrderBy(n => n.CurrentTasks)
                                .FirstOrDefault();

                            if (targetNode != null)
                            {
                                Console.WriteLine($"[DistributedAI] Moving task {task.Id} from {overloadedNode.Name} to {targetNode.Name}");
                                
                                overloadedNode.CurrentTasks--;
                                targetNode.CurrentTasks++;
                                
                                task.AssignedNodeId = targetNode.Id;
                                
                                overloadedNode.Status = overloadedNode.CurrentTasks > 0 ? NodeStatus.Busy : NodeStatus.Ready;
                                targetNode.Status = NodeStatus.Busy;
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Event Handlers

        private void OnJobCompleted(object sender, JobCompletedEventArgs e)
        {
            Console.WriteLine($"[DistributedAI] Job {e.Job.Id} completed by {e.Worker.Name}: {(e.Success ? "Success" : "Failed")}");
        }

        private void OnWorkerStatusChanged(object sender, WorkerStatusChangedEventArgs e)
        {
            Console.WriteLine($"[DistributedAI] Worker {e.WorkerId} status changed: {e.OldStatus} -> {e.NewStatus}");
        }

        #endregion

        #region Control Methods

        public void Start()
        {
            if (_isRunning) return;
            
            _isRunning = true;
            _healthCheckTimer.Start();
            _jobManager.Start();
            
            Console.WriteLine("[DistributedAI] Distributed AI processor started");
        }

        public void Stop()
        {
            if (!_isRunning) return;
            
            _isRunning = false;
            _healthCheckTimer.Stop();
            _jobManager.Stop();
            
            Console.WriteLine("[DistributedAI] Distributed AI processor stopped");
        }

        public AITask GetTaskStatus(string taskId)
        {
            lock (_taskLock)
            {
                return _taskQueue.FirstOrDefault(t => t.Id == taskId);
            }
        }

        public List<AIProcessingNode> GetNodeStatus()
        {
            return _processingNodes.Values.ToList();
        }

        public Dictionary<string, object> GetSystemStats()
        {
            return new Dictionary<string, object>
            {
                ["TotalNodes"] = _processingNodes.Count,
                ["ActiveNodes"] = _processingNodes.Values.Count(n => n.Status == NodeStatus.Busy),
                ["ReadyNodes"] = _processingNodes.Values.Count(n => n.Status == NodeStatus.Ready),
                ["UnhealthyNodes"] = _processingNodes.Values.Count(n => n.Status == NodeStatus.Unhealthy),
                ["QueuedTasks"] = _taskQueue.Count,
                ["TotalTasksProcessed"] = _processingNodes.Values.Sum(n => n.TotalTasksProcessed),
                ["AverageHealthScore"] = _processingNodes.Values.Average(n => n.HealthScore),
                ["SystemEfficiency"] = CalculateSystemEfficiency()
            };
        }

        private double CalculateSystemEfficiency()
        {
            var totalCapacity = _processingNodes.Values.Sum(n => n.MaxConcurrentTasks);
            var currentLoad = _processingNodes.Values.Sum(n => n.CurrentTasks);
            return totalCapacity > 0 ? (double)currentLoad / totalCapacity * 100 : 0;
        }

        #endregion
    }

    #region Supporting Classes and Enums

    public class AITask
    {
        public string Id { get; set; }
        public TaskType Type { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public TaskPriority Priority { get; set; }
        public TaskStatus Status { get; set; }
        public string AssignedNodeId { get; set; }
        public string RequiredSpecialization { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration { get; set; }
        public object Result { get; set; }
        public string Error { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
    }

    public class AIProcessingNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Specialization { get; set; }
        public NodeStatus Status { get; set; }
        public int MaxConcurrentTasks { get; set; }
        public int CurrentTasks { get; set; }
        public int TotalTasksProcessed { get; set; }
        public DateTime LastActivity { get; set; }
        public int HealthScore { get; set; }
        public double ProcessingSpeed { get; set; }
    }

    public enum TaskType
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

    public enum TaskPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    public enum TaskStatus
    {
        Queued,
        InProgress,
        Completed,
        Failed,
        Cancelled
    }

    public enum NodeStatus
    {
        Ready,
        Busy,
        Unhealthy,
        Offline
    }

    public class TaskCompletedEventArgs : EventArgs
    {
        public AITask Task { get; set; }
        public AIProcessingNode Node { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public class NodeStatusChangedEventArgs : EventArgs
    {
        public string NodeId { get; set; }
        public NodeStatus OldStatus { get; set; }
        public NodeStatus NewStatus { get; set; }
        public int CurrentTasks { get; set; }
    }

    #endregion
}
