# Git Commit Convention & Task Tracking Rule

## Mandatory Task Reference Rule

Every git commit in the ShopSphere repository **MUST** reference its associated Notion task ID from the Tasks database. Commits without an explicit `[Task-XX]` reference are prohibited.

---

## Commit Message Format

```text
<type>(<scope>): [Task-XX] <imperative short description>

[optional detailed body explaining context, architecture rationale, or non-obvious decisions]

[optional footer(s)]
```

### Components:

1. **`type`** (required):
   - `feat`: New business feature or domain capability
   - `fix`: Bug fix or error resolution
   - `infra`: Infrastructure, Docker, Terraform, CI/CD, BuildingBlocks setup
   - `refactor`: Code changes that neither fix bugs nor add features
   - `test`: Adding or correcting unit/integration tests
   - `docs`: Documentation, specifications in `docs/specs/`, or ADR updates
   - `chore`: Housekeeping, dependency updates, formatting

2. **`scope`** (required):
   - Must match the target service or layer:
     `buildingblocks` | `catalog` | `cart` | `order` | `inventory` | `payment` | `shipping` | `notification` | `gateway` | `web` | `infra`

3. **`[Task-XX]`** (required):
   - The exact task number from the Notion Tasks database: e.g., `[Task-01]`, `[Task-12]`, `[Task-47]`.

4. **`<description>`** (required):
   - Imperative, present tense (e.g., "add", "implement", "fix", not "added" or "adds").
   - Lowercase first letter, no period at the end.

---

## Examples

### ✅ Valid Commits:
```text
feat(catalog): [Task-12] implement product aggregate root and price versioning
feat(inventory): [Task-26] add reserve inventory consumer with concurrency retry
fix(payment): [Task-34] handle provider timeout by setting state to PaymentUnknown
feat(web): [Task-47] implement visual stepper rendering all 7 order lifecycle states
infra(buildingblocks): [Task-04] add ef core transactional outbox savechanges interceptor
test(order): [Task-44] add testcontainers failure compensation test for stock rejected
```

### ❌ Invalid Commits:
```text
update product catalog                      # Missing type, scope, and Task ID
feat: add product                           # Missing scope and Task ID
feat(catalog): added product entity         # Missing Task ID, non-imperative verb
feat(order): [Task-??] fix checkout bug     # Unknown or missing task ID
```

---

## Workflow Enforcement

1. **Check Task**: Before starting work, ensure the task is marked `In Progress` in the Notion Tasks database.
2. **Implement & Test**: Write code and unit/integration tests covering failure paths.
3. **Run Lint & Health Checks**: Run `bash scripts/lint.sh` (or `bash scripts/fix-lint.sh` to auto-fix) and `bash scripts/doctor.sh` before committing. Ensure 0 errors and 0 warnings.
4. **Commit**: Craft the commit message following the exact format above.
5. **Update Task**: Once PR is merged, transition the task to `Done` in Notion and attach the PR URL.
