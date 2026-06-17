import React from 'react';

interface Props {
  children: React.ReactNode;
  /**
   * Compact mode: render a small inline fallback (for wrapping a single panel/section) with a
   * "Try again" that just resets this boundary — so one panel crashing doesn't blank the whole app.
   * Default (false) = full-screen top-level fallback with reload.
   */
  compact?: boolean;
  /** Optional human label for the failed section (shown in the compact fallback). */
  label?: string;
}

interface State {
  error?: Error;
  componentStack?: string;
}

/**
 * Error boundary. Without this, any render-time throw unmounts the React tree and leaves a blank
 * screen. Top-level (default) shows a full-screen diagnosable fallback + reload. In `compact` mode it
 * wraps a single panel and shows a small inline fallback that resets just that boundary, so a crash in
 * one panel (e.g. preview) degrades gracefully instead of taking down the whole app.
 */
export class ErrorBoundary extends React.Component<Props, State> {
  state: State = {};

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: React.ErrorInfo): void {
    // Surface to console for the dev tools / pure-UI Chrome testing.
    console.error('[ErrorBoundary]', this.props.label ?? '(root)', error, info.componentStack);
  }

  handleReload = (): void => {
    this.setState({ error: undefined, componentStack: undefined });
    window.location.reload();
  };

  // Compact mode: clear the error so the section re-renders, without reloading the whole app.
  handleReset = (): void => {
    this.setState({ error: undefined, componentStack: undefined });
  };

  render(): React.ReactNode {
    const { error } = this.state;
    if (!error) return this.props.children;

    if (this.props.compact) {
      return (
        <div
          role="alert"
          style={{
            padding: 16,
            height: '100%',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 8,
            textAlign: 'center',
            color: 'var(--color-text-secondary, #b0b0b0)',
            fontSize: 12,
          }}
        >
          <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--color-error, #ff4d4f)' }}>
            {this.props.label ? `${this.props.label} failed to render` : 'This panel failed to render'}
          </div>
          <div style={{ fontSize: 12, maxWidth: 320, opacity: 0.8 }}>{error.message}</div>
          <button onClick={this.handleReset} style={{ fontSize: 12, cursor: 'pointer' }}>
            Try again
          </button>
        </div>
      );
    }

    return (
      <div
        role="alert"
        style={{
          padding: 24,
          height: '100vh',
          overflow: 'auto',
          background: 'var(--color-bg, #1f1f1f)',
          color: 'var(--color-text, #e0e0e0)',
          fontSize: 14,
        }}
      >
        <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--color-error, #ff4d4f)', marginBottom: 8 }}>
          Something went wrong
        </div>
        <div style={{ fontSize: 14, marginBottom: 12 }}>{error.message}</div>
        <button
          onClick={this.handleReload}
          style={{ fontSize: 12, marginBottom: 12, cursor: 'pointer' }}
        >
          Reload
        </button>
        <pre style={{ fontSize: 12, opacity: 0.7, whiteSpace: 'pre-wrap' }}>{error.stack}</pre>
      </div>
    );
  }
}
