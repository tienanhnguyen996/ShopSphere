## Changes
<!-- Brief description of what this PR changes -->

## Architecture Checklist
- [ ] Service owns its data (no cross-service DB access)
- [ ] Messages have MessageId, CorrelationId, CausationId, OccurredAtUtc
- [ ] Idempotency key stored with operation
- [ ] Transactional outbox used for writes + publish
- [ ] Timeout handling distinguishes unknown from failure (e.g., PaymentUnknown state)
- [ ] Late events check state/version before applying
- [ ] Concurrency conflicts handled (EF Core tokens/rowversion)
- [ ] Redis used for caching only, not source of truth
- [ ] Tests cover failure paths (out of stock, timeout, late events)

## Testing
<!-- How to verify these changes locally -->
- [ ] Dev container builds
- [ ] `bash scripts/doctor.sh` passes
- [ ] Infrastructure starts: `docker compose -f deploy/compose/docker-compose.yml up -d`
- [ ] Relevant tests pass
