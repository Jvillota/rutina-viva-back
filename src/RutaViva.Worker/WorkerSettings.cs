using System.ComponentModel.DataAnnotations;

namespace RutaViva.Worker;

public sealed class WorkerSettings
{
    public const string SectionName = "Worker";

    /// <summary>How often the outbox is polled for pending events.</summary>
    [Range(1, 3600)]
    public int OutboxPollSeconds { get; set; } = 2;

    [Range(1, 1000)]
    public int OutboxBatchSize { get; set; } = 20;

    /// <summary>How often the worker checks whether the business date changed since the last daily check.</summary>
    [Range(1, 3600)]
    public int DailyCheckPollSeconds { get; set; } = 30;
}
