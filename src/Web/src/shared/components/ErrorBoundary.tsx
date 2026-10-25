import React from 'react';
import { AlertTriangle, RotateCcw } from 'lucide-react';
import { Button } from './Button';

export interface ErrorBoundaryProps {
  children: React.ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
  errorMessage: string;
}

export class ErrorBoundary extends React.Component<ErrorBoundaryProps, ErrorBoundaryState> {
  public constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = {
      hasError: false,
      errorMessage: '',
    };
  }

  public static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return {
      hasError: true,
      errorMessage: error.message || 'An unexpected UI error occurred.',
    };
  }

  private handleReset = (): void => {
    this.setState({ hasError: false, errorMessage: '' });
  };

  public override render(): React.ReactNode {
    if (this.state.hasError) {
      return (
        <div className="min-h-screen bg-gray-50 flex items-center justify-center p-6">
          <div className="max-w-md w-full bg-white rounded-xl shadow-md border border-red-200 p-6 text-center space-y-4">
            <div className="w-12 h-12 rounded-full bg-red-100 text-red-600 flex items-center justify-center mx-auto">
              <AlertTriangle className="w-6 h-6" />
            </div>
            <h2 className="text-lg font-semibold text-gray-900">Something went wrong</h2>
            <p className="text-sm text-gray-600">{this.state.errorMessage}</p>
            <div className="pt-2 flex justify-center">
              <Button variant="primary" size="sm" onClick={this.handleReset}>
                <RotateCcw className="w-4 h-4 mr-1.5" />
                Recover Application
              </Button>
            </div>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
