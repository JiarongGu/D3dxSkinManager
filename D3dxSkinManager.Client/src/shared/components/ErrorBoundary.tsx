import React from 'react';

interface Props {
  children: React.ReactNode;
}

interface State {
  error?: Error;
  componentStack?: string;
}

/**
 * Top-level error boundary. Without this, any render-time throw unmounts the whole React tree and
 * leaves a blank white screen with no information. This catches it and shows the error + component
 * stack so failures are diagnosable (and recoverable via reload) instead of a silent blank app.
 */
export class ErrorBoundary extends React.Component<Props, State> {
  state: State = {};

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: React.ErrorInfo): void {
    // Surface to console for the dev tools / pure-UI Chrome testing.
    console.error('[ErrorBoundary]', error, info.componentStack);
  }

  handleReload = (): void => {
    this.setState({ error: undefined, componentStack: undefined });
    window.location.reload();
  };

  render(): React.ReactNode {
    const { error } = this.state;
    if (!error) return this.props.children;

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
