# Backup and restore

Production backup policy must define owner, encrypted destination, retention, immutability, recovery point objective (RPO), and recovery time objective (RTO). A backup is not trusted until restored and verified.

## Backup

1. Confirm scheduled database backups, point-in-time logs, encryption, retention, and failure alerts.
2. Include required data-protection key material and configuration references through their approved backup mechanisms; never export plaintext secrets.
3. Record backup identifier, UTC range, database version, application/schema version, checksum, and retention class.
4. Restrict access and test restoration on a recurring schedule.

## Restore

1. Declare scope and target recovery point; obtain change approval.
2. Provision an isolated target with compatible engine/version and no outbound integrations.
3. Restore and verify checksums; apply only migrations compatible with the selected application release.
4. Run integrity checks: migration history, organization counts, recent critical records, Identity access, outbox state, and audit continuity.
5. Prevent restored pending jobs/outbox messages from dispatching until reconciliation defines a safe cutoff.
6. For production cutover, stop writes, capture a final recovery point, switch atomically, then monitor.

## Validation and cleanup

Test sign-in, authorization isolation, critical reads/writes, workers, and telemetry. Record achieved RPO/RTO and any loss. Rotate credentials exposed to the exercise, securely destroy temporary copies, and document approval. Never overwrite the only recoverable copy.
