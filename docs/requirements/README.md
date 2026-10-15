# Documentation Structure

This directory contains split requirements from the original Vietnamese specification.

**IMPORTANT FOR AI AGENTS**: Read these files ONLY when needed:

- **01-architecture.md**: Service boundaries, messaging patterns, consistency rules
- **02-services.md**: Service catalog (Catalog, Cart, Order, Inventory, Payment, Shipping, Notification)
- **03-messaging.md**: Message contracts, Saga patterns, failure handling
- **04-infrastructure.md**: Technology stack, deployment, observability

## Context Budget

Total: ~700 lines across 4 files. Read selectively:
- Working on service design? → Read 02-services.md + 03-messaging.md
- Working on infrastructure? → Read 04-infrastructure.md
- Working on API boundaries? → Read 01-architecture.md

The root **AGENTS.md** provides the condensed architectural rules (<150 lines). Start there before reading detailed requirements.

## Removed Files

- `00-source.md` (English original)
- `00-source.vi.md` (Vietnamese original)

Both were split into 01-04 and removed to prevent context duplication.
