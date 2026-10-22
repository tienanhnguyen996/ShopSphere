# Agent and MCP Setup Guide

This guide covers MCP (Model Context Protocol) server configuration for AI agents working with the ShopSphere monorepo.

## Overview

The repository includes `.mcp.json` which works automatically with **Claude Code**. Other environments (such as Antigravity) use centralized MCP configurations.

## MCP Servers Used

| Server | Purpose | Transport | Authentication |
|--------|---------|-----------|----------------|
| **github** | PR/issue management, repo operations | Docker (`ghcr.io/github/github-mcp-server:latest`) | Fine-grained PAT |
| **notion** | Docs/specs management | stdio (`@suekou/mcp-notion-server@2.0.2`) | Integration Token |
| **context7** | Codebase context indexing | stdio (`@upstash/context7-mcp@4.4.2`) | None (local) |
| **microsoft-learn** | .NET/Azure documentation | HTTP (`https://learn.microsoft.com/api/mcp`) | None (public) |
| **terraform** | Infrastructure inspection (read-only) | Docker (`hashicorp/terraform-mcp-server:latest`) | None |
| **postman** *(Optional)* | API exploration and debugging | stdio (`@postman/postman-mcp-server@2.13.0`) | API Key |

## Claude Code

Auto-configured via `.mcp.json` in the repository root.

**Setup**:
1. Copy `.env.example` to `.env`
2. Add your tokens:
   ```bash
   GITHUB_TOKEN=github_pat_...
   NOTION_TOKEN=secret_...
   ```
3. Authenticate GitHub CLI:
   ```bash
   gh auth login
   ```

## Antigravity

Antigravity uses a centralized MCP configuration. Configure servers in your agent settings or MCP configuration:

**Example configuration**:
```json
{
  "servers": {
    "github": {
      "type": "stdio",
      "command": "docker",
      "args": [
        "run",
        "-i",
        "--rm",
        "-e",
        "GITHUB_PERSONAL_ACCESS_TOKEN",
        "ghcr.io/github/github-mcp-server:latest"
      ],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "${GITHUB_TOKEN}"
      }
    },
    "notion": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@suekou/mcp-notion-server@2.0.2"],
      "env": {
        "NOTION_API_TOKEN": "${NOTION_TOKEN}"
      }
    },
    "context7": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@upstash/context7-mcp@4.4.2"]
    },
    "terraform": {
      "type": "stdio",
      "command": "docker",
      "args": [
        "run",
        "-i",
        "--rm",
        "-v",
        "${PWD}/deploy/terraform:/workspace",
        "hashicorp/terraform-mcp-server:latest"
      ]
    }
  }
}
```

## Token Creation

### GitHub Fine-Grained Personal Access Token

Use a fine-grained token scoped only to the ShopSphere repository with least-privilege permissions:

1. Visit https://github.com/settings/personal-access-tokens/new
2. Name: "ShopSphere AI Agent"
3. Expiration: 90 days (rotate regularly)
4. Repository access: **Only select repositories** -> select `ShopSphere`
5. Permissions:
   - **Contents**: Read and write
   - **Pull requests**: Read and write
   - **Issues**: Read and write
   *(Do not include `workflow` unless you explicitly manage Actions workflow files)*
6. Generate and copy token (starts with `github_pat_` or classic `ghp_`)
7. Add to `.env`: `GITHUB_TOKEN=github_pat_...`

### Notion Integration Token

1. Go to https://www.notion.so/my-integrations
2. Click "+ New integration"
3. Name: "ShopSphere Docs"
4. Associated workspace: Your workspace
5. Capabilities: Read content, Update content, Insert content
6. Submit -> Copy "Internal Integration Secret" (starts with `secret_`)
7. Add to `.env`: `NOTION_TOKEN=secret_...`
8. **Share pages**: Go to Notion pages/databases you want accessible, click Share, invite the integration

### Postman API Key (Optional)

Postman is optional. OpenAPI is the single source of truth for all API contracts. If using Postman:
1. Go to https://web.postman.co/settings/me/api-keys
2. Click "Generate API Key"
3. Add to `.env`: `POSTMAN_API_KEY=PMAK-...`
4. Set workspace name: `POSTMAN_WORKSPACE_NAME=ShopSphere`

## Verification

Test MCP servers:

**GitHub**:
```bash
gh pr list
```

**Notion**:
Test querying or listing pages via the Notion tool.

**Context7**:
Verify documentation lookup for libraries via context7 tool.

**Terraform**:
Run `terraform plan` or inspect modules via the terraform server.

## Security Notes

- **Never commit tokens** to git (`.env` is gitignored)
- Rotate tokens every 90 days
- Use fine-grained least-privilege permissions
- Each team member creates their own tokens
