namespace D3dxSkinManager.Modules.Core.Event
{

    /// <summary>
    /// Event message structure for event bus communication.
    /// Follows the same pattern as IpcRequest for consistency.
    /// </summary>
    public class EventMessage
    {
        /// <summary>
        /// Unique identifier for the event
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Module name that emitted the event (e.g., "CORE", "MOD", "TASK_QUEUE", "DROP_ZONE")
        /// </summary>
        public string Module { get; set; } = string.Empty;

        /// <summary>
        /// Event type/name (SCREAMING_SNAKE_CASE string)
        /// Examples: "APPLICATION_STARTED", "MOD_LOADED", "TASK_ADDED", "CLICK"
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Optional ProfileId for profile-scoped events
        /// Null/empty for global events (e.g., APPLICATION_STARTED)
        /// Set for profile-specific events (e.g., MOD_LOADED in specific profile)
        /// </summary>
        public string? ProfileId { get; set; }

        /// <summary>
        /// Event payload data
        /// </summary>
        public object? Payload { get; set; }

        /// <summary>
        /// Event timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
