# ShopSphere Agent Instructions

For architecture and workflow rules, see [AGENTS.md](./AGENTS.md).

**Quick summary**:
- Each service owns its data (no cross-service DB access)
- Use Transactional Outbox for writes + messages
- Timeout = unknown result (not failure) — query before retry
- Redis is cache only, not source of truth
- Test all failure paths
- Run `bash scripts/doctor.sh` before committing
