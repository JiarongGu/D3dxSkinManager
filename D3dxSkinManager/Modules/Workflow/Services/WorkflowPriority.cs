namespace D3dxSkinManager.Modules.Workflow.Services;

/// <summary>
/// Admission priority for a queued import job. When the <see cref="IImportQueueActor"/> frees a slot it
/// admits the highest-priority queued job: CONFIRMED imports (user hit confirm → actually importing)
/// before unconfirmed previews, then HIGHER progress first, then EARLIER-created first — so confirmed +
/// more-progressed + earlier-added float to the front (ordered by <see cref="WorkflowPriorityComparer"/>).
/// </summary>
public readonly record struct WorkflowPriority(bool Confirmed, int Progress, DateTime CreatedAtUtc);
