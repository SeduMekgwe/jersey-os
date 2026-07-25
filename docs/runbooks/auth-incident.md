# Authentication incident

Use for suspected credential stuffing, signing-key exposure, refresh-token theft/reuse, widespread login failure, or unauthorized sessions.

## Triage

1. Open an incident; record UTC start, symptoms, affected environments and organizations.
2. Determine whether impact is confidentiality, integrity, or availability.
3. Inspect aggregate authentication outcomes, reuse detections, lockouts, issuer/audience errors, key identifiers, and deployment changes. Do not inspect raw tokens.
4. Distinguish isolated account compromise from platform-wide key or validation failure.

## Containment

- Isolated account: disable or protect the account, revoke all refresh-token families, require credential reset, and preserve audit evidence.
- Refresh-token replay: revoke the complete family and active sessions; investigate client compromise.
- Signing-key exposure: disable issuance if needed, rotate the key through the approved provider, reject the compromised key, revoke refresh sessions, and communicate forced reauthentication.
- Credential attack: tighten rate controls and block abusive sources without revealing account existence.
- Availability-only regression: roll back the responsible change if safe; do not weaken validation.

## Recovery

Verify issuance and validation from clean clients, revoked-token rejection, permission enforcement, and normalized error/latency rates. Notify affected parties through approved channels where required.

## Follow-up

Preserve audit records and a timeline, scope data access, satisfy breach-notification obligations, rotate related secrets, and create corrective actions. Never restore trust to a compromised key.
