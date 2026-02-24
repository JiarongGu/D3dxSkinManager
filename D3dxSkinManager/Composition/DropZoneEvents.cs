namespace D3dxSkinManager.Composition;

/// <summary>
/// Drop zone event type constants for IPC notifications.
/// </summary>
public static class DropZoneEvents
{
    /// <summary>
    /// Fired when drop zone is clicked.
    /// </summary>
    public const string CLICK = "DROP_ZONE_CLICK";

    /// <summary>
    /// Fired when drag enters a drop zone.
    /// </summary>
    public const string DRAG_ENTER = "DROP_ZONE_DRAG_ENTER";

    /// <summary>
    /// Fired when drag leaves a drop zone.
    /// </summary>
    public const string DRAG_LEAVE = "DROP_ZONE_DRAG_LEAVE";

    /// <summary>
    /// Fired when files are dropped on a drop zone.
    /// </summary>
    public const string FILE_DROP = "DROP_ZONE_FILE_DROP";

    /// <summary>
    /// Fired when mouse enters a drop zone (non-dragging hover).
    /// </summary>
    public const string MOUSE_ENTER = "DROP_ZONE_MOUSE_ENTER";

    /// <summary>
    /// Fired when mouse leaves a drop zone (non-dragging hover).
    /// </summary>
    public const string MOUSE_LEAVE = "DROP_ZONE_MOUSE_LEAVE";
}
