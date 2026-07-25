import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Button } from '@/components/ui';
export class ErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unhandled application error', error, info);
  }
  render() {
    if (this.state.failed)
      return (
        <main className="grid min-h-screen place-items-center p-6">
          <div className="max-w-md text-center">
            <h1 className="text-2xl font-semibold">Something went wrong</h1>
            <p className="mt-2 text-muted-foreground">
              The application encountered an unexpected error.
            </p>
            <Button
              className="mt-6"
              onClick={() => {
                window.location.assign('/');
              }}
            >
              Reload application
            </Button>
          </div>
        </main>
      );
    return this.props.children;
  }
}
