# ADR 0001: MassTransit Version Selection

## Status

Accepted — 2026-10-15

## Context

ShopSphere requires a robust message broker abstraction for implementing:
- Transactional Outbox pattern
- Saga orchestration (checkout workflow)
- Consumer Inbox/deduplication
- Retry and error handling

MassTransit is the leading .NET library for these patterns. However, **MassTransit v9+ is commercial software** requiring licensing fees. The project needs to decide between:

1. **v8.x** (Apache 2.0, fully open source)
2. **v9.x+** (commercial license required)

## Decision

**Use MassTransit 8.3.3** (latest stable v8.x release)

Lock the version in `Directory.Packages.props`:
```xml
<PackageVersion Include="MassTransit" Version="8.3.3" />
<PackageVersion Include="MassTransit.EntityFrameworkCore" Version="8.3.3" />
<PackageVersion Include="MassTransit.RabbitMQ" Version="8.3.3" />
```

## Rationale

### Advantages of v8.3.3

- ✅ **Apache 2.0 license**: No licensing costs, no usage restrictions
- ✅ **Proven stability**: v8 has been in production use for 2+ years
- ✅ **Complete feature set**: Supports all patterns needed for ShopSphere:
  - Transactional Outbox (Bus Outbox and Consumer Outbox)
  - State Machine Sagas
  - Consumer retry and error handling
  - RabbitMQ integration
  - Request/Response patterns
- ✅ **Extensive documentation**: Mature docs and community examples
- ✅ **Long-term maintenance**: v8.x continues to receive security patches

### Trade-offs

- ❌ **No v9+ features**: Missing performance improvements and newer patterns
- ❌ **Future migration path**: If v9+ becomes necessary, migration will require:
  - License procurement
  - Code review for breaking changes
  - Testing across all services

However, **v8.3.3 meets all current requirements**. The missing v9+ features are optimizations, not blockers.

## Consequences

### Positive

- No upfront or recurring licensing costs
- Full feature access without restrictions
- Stable, battle-tested codebase
- Clear legal/compliance posture (open source)

### Negative

- If v9+ features become critical (performance at scale, specific new patterns), will require migration
- Community focus shifts to v9+ over time (though v8 docs remain)

### Migration Path (if needed)

If upgrading to v9+ becomes necessary:

1. **Evaluate license**: Commercial pricing, terms, compliance review
2. **ADR for upgrade**: Document why v8 no longer meets needs
3. **Review breaking changes**: MassTransit v8→v9 migration guide
4. **Test in staging**: Validate Outbox, Saga, retry behavior
5. **Phased rollout**: One service at a time, not big-bang

## Alternatives Considered

### Alternative 1: Use v9+ immediately

**Rejected** because:
- Adds licensing cost without clear value over v8
- v8 features are sufficient for all current requirements
- Adds procurement/budgeting delay to project start

### Alternative 2: Use NServiceBus

**Rejected** because:
- Also commercial (Particular Software licensing)
- More enterprise-focused, heavier footprint
- Smaller community in .NET ecosystem vs. MassTransit

### Alternative 3: Roll our own abstraction

**Rejected** because:
- Massive scope (Outbox, Saga, retry, error handling are non-trivial)
- Reinventing mature patterns
- No team expertise in message broker internals

MassTransit v8 is the pragmatic choice.

## References

- [MassTransit Documentation](https://masstransit.io/)
- [MassTransit v8 GitHub](https://github.com/MassTransit/MassTransit/tree/v8)
- [MassTransit v9 Commercial Announcement](https://github.com/MassTransit/MassTransit/discussions/4169)
- [Apache 2.0 License](https://www.apache.org/licenses/LICENSE-2.0)
- [Package lock in Directory.Packages.props](../../Directory.Packages.props)

## Review

- **Revisit**: When scaling requires v9+ features, or when v8 security support ends
- **Owner**: Architecture team
