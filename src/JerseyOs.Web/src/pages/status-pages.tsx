import { Link } from 'react-router-dom';
import { Button } from '@/components/ui';
export function UnauthorizedPage() {
  return (
    <main className="grid min-h-screen place-items-center p-6 text-center">
      <div>
        <p className="text-sm font-medium text-primary">403</p>
        <h1 className="mt-2 text-3xl font-semibold">Access denied</h1>
        <p className="mt-3 text-muted-foreground">
          Your account does not have permission to view this page.
        </p>
        <Button asChild className="mt-6">
          <Link to="/">Return to dashboard</Link>
        </Button>
      </div>
    </main>
  );
}
export function NotFoundPage() {
  return (
    <main className="grid min-h-screen place-items-center p-6 text-center">
      <div>
        <p className="text-sm font-medium text-primary">404</p>
        <h1 className="mt-2 text-3xl font-semibold">Page not found</h1>
        <p className="mt-3 text-muted-foreground">The requested page does not exist.</p>
        <Button asChild className="mt-6">
          <Link to="/">Return to dashboard</Link>
        </Button>
      </div>
    </main>
  );
}
