using D3dxSkinManager.Modules.Core.Helpers;
using System.Collections.Concurrent;

namespace D3dxSkinManager.Infrastructure.WebView
{
    public sealed class WebViewSessionManager
    {
        private readonly ConcurrentDictionary<string, WebViewSession> _sessions = new();
        private readonly ILogHelper _logger;

        public WebViewSessionManager(ILogHelper logger)
        {
            _logger = logger;
        }

        public ICollection<WebViewSession> Sessions => _sessions.Values;

        public WebViewSession Create(
            string sessionId,
            Func<WebViewSession> factory)
        {
            var session = factory();
            if (!_sessions.TryAdd(sessionId, session))
                throw new InvalidOperationException($"Session already exists: {sessionId}");

            return session;
        }

        public bool TryGet(string sessionId, out WebViewSession session)
            => _sessions.TryGetValue(sessionId, out session!);

        public void Remove(string sessionId)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                session.Dispose();
                _logger.Info($"Removed session: {sessionId}", "Host");
            }
        }

        // Broadcast backend -> frontends (gated by each session's subscriptions)
        public void BroadcastNotification(string module, string type, object? payload = null)
        {
            foreach (var s in _sessions.Values)
            {
                s.Ipc.SendNotification(module, type, payload);
            }
        }
    }
}
