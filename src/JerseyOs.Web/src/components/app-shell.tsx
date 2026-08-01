import * as Dialog from '@radix-ui/react-dialog';
import { Activity, LayoutDashboard, LogOut, Menu, Package, ShieldCheck, Tags, X } from 'lucide-react';
import { useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '@/auth/auth-context';
import { Button } from '@/components/ui';
import { cn } from '@/lib/cn';

const nav = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, end: true },
  { to: '/products', label: 'Products', icon: Package, end: false },
  { to: '/taxonomy', label: 'Taxonomy', icon: Tags, end: false },
  { to: '/system-health', label: 'System health', icon: Activity, end: false },
];
function Navigation({ onNavigate }: { onNavigate?: () => void }) {
  const { logout } = useAuth();
  return (
    <>
      <div className="mb-8 flex h-12 items-center gap-2 text-lg font-semibold">
        <ShieldCheck className="text-primary" /> Jersey OS
      </div>
      <nav className="space-y-1">
        {nav.map(({ icon: Icon, to, label, end }) => (
          <NavLink
            key={to}
            to={to}
            end={end}
            onClick={onNavigate}
            className={({ isActive }) =>
              cn(
                'flex items-center gap-3 rounded-md px-3 py-2 text-sm',
                isActive
                  ? 'bg-accent text-accent-foreground'
                  : 'text-muted-foreground hover:bg-accent',
              )
            }
          >
            <Icon size={18} />
            {label}
          </NavLink>
        ))}
      </nav>
      <Button
        variant="ghost"
        className="absolute bottom-4 left-4 right-4 justify-start gap-3"
        onClick={() => void logout()}
      >
        <LogOut size={18} />
        Sign out
      </Button>
    </>
  );
}

export function AppShell() {
  const [open, setOpen] = useState(false);
  const { user } = useAuth();
  return (
    <Dialog.Root open={open} onOpenChange={setOpen}>
      <div className="min-h-screen bg-background text-foreground">
        <a
          href="#main-content"
          className="sr-only z-50 rounded-md bg-primary px-4 py-2 text-primary-foreground focus:not-sr-only focus:fixed focus:left-4 focus:top-4"
        >
          Skip to content
        </a>
        <header className="fixed inset-x-0 top-0 z-30 flex h-16 items-center border-b bg-background/95 px-4 backdrop-blur lg:pl-72">
          <Dialog.Trigger asChild>
            <Button
              variant="ghost"
              size="icon"
              className="mr-3 lg:hidden"
              aria-label="Open navigation"
            >
              <Menu />
            </Button>
          </Dialog.Trigger>
          <div className="flex flex-1 items-center justify-between">
            <span className="font-semibold">Product catalog</span>
            <div className="text-right">
              <p className="text-sm font-medium">{user?.displayName}</p>
              <p className="text-xs text-muted-foreground">{user?.email}</p>
            </div>
          </div>
        </header>
        <aside
          aria-label="Primary navigation"
          className="fixed inset-y-0 left-0 z-40 hidden w-64 border-r bg-card p-4 lg:block"
        >
          <Navigation />
        </aside>
        <Dialog.Portal>
          <Dialog.Overlay className="fixed inset-0 z-40 bg-black/60 lg:hidden" />
          <Dialog.Content
            className="fixed inset-y-0 left-0 z-50 w-64 border-r bg-card p-4 shadow-xl lg:hidden"
            aria-describedby={undefined}
          >
            <Dialog.Title className="sr-only">Primary navigation</Dialog.Title>
            <Dialog.Close asChild>
              <Button
                variant="ghost"
                size="icon"
                className="absolute right-3 top-3"
                aria-label="Close navigation"
              >
                <X />
              </Button>
            </Dialog.Close>
            <Navigation
              onNavigate={() => {
                setOpen(false);
              }}
            />
          </Dialog.Content>
        </Dialog.Portal>
        <main
          id="main-content"
          tabIndex={-1}
          className="min-h-screen px-4 pb-10 pt-24 lg:pl-72 lg:pr-8"
        >
          <Outlet />
        </main>
      </div>
    </Dialog.Root>
  );
}
