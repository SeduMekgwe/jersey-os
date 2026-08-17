import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Button, Card, Input } from '@/components/ui';
import { apiRequest } from '@/lib/api';
import type {
  CreatedOrganizationInvitationDto,
  OrganizationInvitationDto,
} from '@/types/api';

export function MembersPage() {
  const queryClient = useQueryClient();
  const [email, setEmail] = useState('');
  const [role, setRole] = useState('Member');
  const [plaintext, setPlaintext] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const invitationsQuery = useQuery({
    queryKey: ['org', 'invitations'],
    queryFn: () => apiRequest<OrganizationInvitationDto[]>('/org/invitations'),
  });

  const create = useMutation({
    mutationFn: () =>
      apiRequest<CreatedOrganizationInvitationDto>('/org/invitations', {
        method: 'POST',
        body: JSON.stringify({ email, role }),
      }),
    onSuccess: (created) => {
      setPlaintext(created.plaintext);
      setError(null);
      setEmail('');
      void queryClient.invalidateQueries({ queryKey: ['org', 'invitations'] });
    },
    onError: (err: Error) => setError(err.message),
  });

  const revoke = useMutation({
    mutationFn: (id: string) =>
      apiRequest<OrganizationInvitationDto>(`/org/invitations/${id}/revoke`, { method: 'POST' }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['org', 'invitations'] });
    },
    onError: (err: Error) => setError(err.message),
  });

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">Members</h1>
        <p className="mt-2 text-muted-foreground">
          Invite operators. The invitation token is shown once.
        </p>
      </div>
      {error && (
        <Card className="border-destructive/50 p-4" role="alert">
          {error}
        </Card>
      )}
      {plaintext && (
        <Card className="space-y-2 p-4" role="status">
          <p className="font-medium">Copy this invitation link now. The token will not be shown again.</p>
          <code className="block break-all text-sm">{`${window.location.origin}/invite/${plaintext}`}</code>
        </Card>
      )}
      <Card className="space-y-3 p-4">
        <label className="block text-sm">
          Email
          <Input
            className="mt-1"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </label>
        <label className="block text-sm">
          Role
          <select
            className="mt-1 flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            value={role}
            onChange={(e) => setRole(e.target.value)}
          >
            <option value="Member">Member</option>
            <option value="Admin">Admin</option>
          </select>
        </label>
        <Button disabled={create.isPending || !email} onClick={() => create.mutate()}>
          Send invitation
        </Button>
      </Card>
      <div className="space-y-3">
        {(invitationsQuery.data ?? []).map((invitation) => (
          <Card key={invitation.id} className="flex flex-wrap items-center justify-between gap-3 p-4">
            <div>
              <p className="font-medium">
                {invitation.email} · {invitation.role}
              </p>
              <p className="text-sm text-muted-foreground">
                Expires {new Date(invitation.expiresAtUtc).toLocaleString()}
                {invitation.acceptedAtUtc ? ' · accepted' : ''}
                {invitation.revokedAtUtc ? ' · revoked' : ''}
              </p>
            </div>
            {!invitation.acceptedAtUtc && !invitation.revokedAtUtc && (
              <Button
                variant="outline"
                disabled={revoke.isPending}
                onClick={() => revoke.mutate(invitation.id)}
              >
                Revoke
              </Button>
            )}
          </Card>
        ))}
        {invitationsQuery.isSuccess && invitationsQuery.data.length === 0 && (
          <p className="text-sm text-muted-foreground">No invitations yet.</p>
        )}
      </div>
    </div>
  );
}
