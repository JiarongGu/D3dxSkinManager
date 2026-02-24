namespace D3dxSkinManager.Modules.Core.Event
{

    /// <summary>
    /// Event arguments passed to plugin event handlers.
    /// </summary>
    public class EventMessage
    {
        /// <summary>
        /// Event type constant (SCREAMING_SNAKE_CASE string)
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Optional event name for CUSTOM_EVENT types
        /// </summary>
        public string? EventName { get; set; }

        /// <summary>
        /// Event data payload
        /// </summary>
        public object? Data { get; set; }

        /// <summary>
        /// Event timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
