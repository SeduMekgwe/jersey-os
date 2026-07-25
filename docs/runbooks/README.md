# Operational runbooks

Runbooks are executable guidance, not substitutes for access control or judgment.

- [Local startup](local-startup.md)
- [Authentication incident](auth-incident.md)
- [Background job failure](job-failure.md)
- [Backup and restore](backup-restore.md)

## Incident discipline

1. Assign incident lead, severity, and communications owner.
2. Preserve timestamps, correlation IDs, affected organizations, and actions.
3. Stabilize service before deep diagnosis; prefer reversible mitigations.
4. Do not paste secrets or personal data into chat, tickets, or logs.
5. Validate recovery with user-visible signals and telemetry.
6. Record follow-up owners and write a blameless review for material incidents.

Environment-specific endpoints, dashboards, contacts, and access procedures must be added before production launch and reviewed quarterly.
