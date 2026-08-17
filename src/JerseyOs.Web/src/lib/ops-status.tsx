import { HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useAuth } from '@/auth/auth-context';
import { tokenStore } from '@/lib/api';

interface OpsStatusEvent {
  organizationId: string;
  kind: string;
  entityId: string;
  status: string;
  occurredAtUtc: string;
}

export function OpsStatusListener() {
  const { user } = useAuth();
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!user) {
      return;
    }

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/ops', { accessTokenFactory: () => tokenStore.get() ?? '' })
      .withAutomaticReconnect()
      .build();

    connection.on('opsStatus', (event: OpsStatusEvent) => {
      if (event.kind === 'import.batch' || event.kind === 'import.scrape') {
        void queryClient.invalidateQueries({ queryKey: ['import'] });
      } else if (event.kind === 'publishing.run') {
        void queryClient.invalidateQueries({ queryKey: ['publishing'] });
      } else if (event.kind === 'ai.generation') {
        void queryClient.invalidateQueries({ queryKey: ['ai'] });
      }
    });

    void connection.start().catch(() => {
      /* polling remains the fallback */
    });

    return () => {
      connection.off('opsStatus');
      if (connection.state !== HubConnectionState.Disconnected) {
        void connection.stop();
      }
    };
  }, [user, queryClient]);

  return null;
}
