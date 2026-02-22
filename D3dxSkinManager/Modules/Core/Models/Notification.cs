using System;
using System.Collections.Generic;
using System.Text;

namespace D3dxSkinManager.Modules.Core.Models
{
    /// <summary>
    /// Notification types for operation-related events
    /// </summary>
    public enum NotificationType
    {
        /// <summary>Operation was started</summary>
        OperationStarted,

        /// <summary>Progress update (percentage or step change)</summary>
        ProgressUpdate,

        /// <summary>Operation completed successfully</summary>
        OperationCompleted,

        /// <summary>Operation failed with error</summary>
        OperationFailed,

        /// <summary>Operation was cancelled</summary>
        OperationCancelled
    }

    /// <summary>
    /// Notification payload for operation events
    /// Sent via IPC to frontend
    /// </summary>
    public class Notification
    {
        /// <summary>Type of notification</summary>
        public NotificationType Type { get; set; }

        /// <summary>Operation progress data</summary>
        public OperationProgress Operation { get; set; } = new();

        /// <summary>Timestamp of notification</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
