namespace D3dxSkinManager.Modules.Core.Event
{
    /// <summary>
    /// System event types that plugins can listen to.
    /// </summary>
    public enum EventType
    {
        /// <summary>
        /// Fired when application starts up (after plugin initialization).
        /// </summary>
        ApplicationStarted,

        /// <summary>
        /// Fired when application is shutting down.
        /// </summary>
        ApplicationShutdown,

        /// <summary>
        /// Fired when a mod is loaded into the game.
        /// </summary>
        ModLoaded,

        /// <summary>
        /// Fired when a mod is unloaded from the game.
        /// </summary>
        ModUnloaded,

        /// <summary>
        /// Fired when a mod is deleted.
        /// </summary>
        ModDeleted,

        /// <summary>
        /// Fired when a new mod is imported.
        /// </summary>
        ModImported,

        /// <summary>
        /// Fired when mod data is refreshed.
        /// </summary>
        ModsRefreshed,

        /// <summary>
        /// Fired when classification tree is updated.
        /// </summary>
        ClassificationTreeChanged,

        /// <summary>
        /// Custom event emitted by other plugins.
        /// </summary>
        CustomEvent
    }
}
