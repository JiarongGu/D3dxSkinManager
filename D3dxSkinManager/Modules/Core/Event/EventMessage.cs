using System;
using System.Collections.Generic;
using System.Text;

namespace D3dxSkinManager.Modules.Core.Event
{

    /// <summary>
    /// Event arguments passed to plugin event handlers.
    /// </summary>
    public class EventMessage
    {
        public EventType EventType { get; set; }
        public string? EventName { get; set; }  // For CustomEvent type
        public object? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
