namespace D3dxSkinManager.Modules.Core.Event
{

    /// <summary>
    /// Event message for pub/sub communication via EventBus.
    /// </summary>
    public class EventMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Module name (e.g., "MOD", "PROFILE", "TASK_QUEUE")</summary>
        public string Module { get; set; } = string.Empty;

        /// <summary>Event type in SCREAMING_SNAKE_CASE (e.g., "LOADED", "DELETED")</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>Profile ID for profile-scoped events, null for global events</summary>
        public string? ProfileId { get; set; }

        public object? Payload { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
