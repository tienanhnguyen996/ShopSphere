# Agent and MCP Setup Guide

This guide covers MCP (Model Context Protocol) server configuration for different AI agents working with the ShopSphere monorepo.

## Overview

The repository includes `.mcp.json` which works automatically with **Claude Code**. Other agents (Cline, Windsurf, Antigravity, Cursor) require manual MCP configuration.

## MCP Servers Used

| Server | Purpose | Authentication |
|--------|---------|----------------|
| **GitHub** | PR/issue management, repo operations | Personal Access Token |
| **Notion** | Docs/specs management | Integration Token |
| **Postman** | API documentation and collection management | API Key |
| **Context7** | Codebase context indexing | None (local) |
| **Microsoft Learn** | .NET/Azure documentation | None |
| **Terraform** | Infrastructure inspection | None (read-only) |

## Claude Code

✅ **Already configured** via `.mcp.json` in the repository root.

**Setup**:
1. Copy `.env.example` to `.env`
2. Add your tokens:
   ```bash
   GITHUB_TOKEN=ghp_...
   NOTION_TOKEN=secret_...
   POSTMAN_API_KEY=PMAK-...
   POSTMAN_WORKSPACE_NAME=ShopSphere
   ```
3. Authenticate GitHub CLI:
   ```bash
   gh auth login
   ```

## Cline (VS Code Extension)

Add to VS Code settings (`.vscode/settings.json` or user settings):

```json
{
  "cline.mcpServers": {
    "github": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github@0.5.0"],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "${env:GITHUB_TOKEN}"
      }
    },
    "notion": {
      "command": "npx",
      "args": ["-y", "@suekou/mcp-notion-server"],
      "env": {
        "NOTION_API_TOKEN": "${env:NOTION_TOKEN}"
      }
    },
    "postman": {
      "command": "npx",
      "args": ["-y", "@postman/postman-mcp-server@2.13.0"],
      "env": {
        "POSTMAN_API_KEY": "${env:POSTMAN_API_KEY}",
        "POSTMAN_WORKSPACE_NAME": "${env:POSTMAN_WORKSPACE_NAME}"
      }
    },
    "context7": {
      "command": "npx",
      "args": ["-y", "@context7/mcp-server@latest"]
    }
  }
}
```

## Windsurf (IDE)

Add to Windsurf MCP configuration file (`~/.windsurf/mcp-config.json`):

```json
{
  "mcpServers": {
    "github": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github@0.5.0"],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "ghp_YOUR_TOKEN"
      }
    },
    "notion": {
      "command": "npx",
      "args": ["-y", "@suekou/mcp-notion-server"],
      "env": {
        "NOTION_API_TOKEN": "secret_YOUR_TOKEN"
      }
    },
    "postman": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-postman@0.1.0"],
      "env": {
        "POSTMAN_API_KEY": "PMAK_YOUR_KEY",
        "POSTMAN_WORKSPACE_NAME": "ShopSphere"
      }
    },
    "terraform": {
      "command": "docker",
      "args": [
        "run", "-i", "--rm",
        "-v", "${PWD}/deploy/terraform:/workspace",
        "mcp/terraform:0.2.0"
      ]
    }
  }
}
```

**Note**: Replace `YOUR_TOKEN` with actual tokens or use environment variable interpolation if supported.

## Cursor (IDE)

Add to Cursor settings (`.cursor/mcp.json` in project or `~/.cursor/mcp.json` globally):

```json
{
  "mcpServers": {
    "github": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github@0.5.0"],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "ghp_YOUR_TOKEN"
      }
    },
    "postman": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-postman@0.1.0"],
      "env": {
        "POSTMAN_API_KEY": "PMAK_YOUR_KEY",
        "POSTMAN_WORKSPACE_NAME": "ShopSphere"
      }
    },
    "context7": {
      "command": "npx",
      "args": ["-y", "@context7/mcp-server@latest"]
    }
  }
}
```

## Antigravity

Antigravity uses a centralized MCP configuration. Add servers through the UI or config file (location varies by installation).

**Example configuration**:
```json
{
  "servers": {
    "github": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github@0.5.0"],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "ghp_YOUR_TOKEN"
      }
    },
    "notion": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@suekou/mcp-notion-server"],
      "env": {
        "NOTION_API_TOKEN": "secret_YOUR_TOKEN"
      }
    },
    "postman": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-postman@0.1.0"],
      "env": {
        "POSTMAN_API_KEY": "PMAK_YOUR_KEY",
        "POSTMAN_WORKSPACE_NAME": "ShopSphere"
      }
    },
    "context7": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@context7/mcp-server@latest"]
    }
  }
}
```

## Token Creation

### GitHub Personal Access Token

1. Go to https://github.com/settings/tokens/new
2. Name: "ShopSphere AI Agent"
3. Scopes: `repo`, `read:org`, `workflow` (for CI workflows)
4. Generate and copy token (starts with `ghp_`)
5. Add to `.env`: `GITHUB_TOKEN=ghp_...`

### Notion Integration Token

1. Go to https://www.notion.so/my-integrations
2. Click "+ New integration"
3. Name: "ShopSphere Docs"
4. Associated workspace: Your workspace
5. Capabilities: Read content, Update content, Insert content
6. Submit → Copy "Internal Integration Secret" (starts with `secret_`)
7. Add to `.env`: `NOTION_TOKEN=secret_...`
8. **Share pages**: Go to Notion pages/databases you want accessible, click Share, invite the integration

### Postman API Key

1. Go to https://web.postman.co/settings/me/api-keys
2. Click "Generate API Key"
3. Name: "ShopSphere AI Agent"
4. Copy the key (starts with `PMAK-`)
5. Add to `.env`: `POSTMAN_API_KEY=PMAK-...`
6. Set workspace name: `POSTMAN_WORKSPACE_NAME=ShopSphere`
7. Create the workspace in Postman if it doesn't exist

## Verification

Test MCP servers:

**GitHub**:
```bash
# Claude Code / Cline
Ask agent: "List open PRs in this repo"

# Manual test
gh pr list
```

**Notion**:
```bash
# Ask agent: "Show me the page titled 'API Design'"
# Should retrieve content if integration has access
```

**Context7**:
```bash
# Ask agent: "What services exist in src/Services/?"
# Should index and return service structure
```

**Terraform**:
```bash
# Ask agent: "Show me the Terraform modules"
# Should list modules from deploy/terraform/
```

**Postman**:
```bash
# Ask agent: "List collections in ShopSphere workspace"
# Should return existing collections
```

## Troubleshooting

**"Server not found" or "Connection failed"**:
1. Check tool installed: `npx --version`, `docker --version`
2. Verify tokens in `.env`
3. Restart IDE/agent
4. Check MCP config syntax (valid JSON, correct paths)

**"Permission denied" (GitHub)**:
1. Verify token scopes: `gh auth status`
2. Re-authenticate: `gh auth login`
3. Check token hasn't expired

**"Not shared" (Notion)**:
1. Open Notion page → Share → Invite your integration
2. Repeat for each page/database the agent needs access to

**"Workspace not found" (Postman)**:
1. Check workspace name matches exactly (case-sensitive)
2. Verify API key has workspace access
3. Create workspace if it doesn't exist

**Terraform mount fails (Windows)**:
- Use Git Bash or WSL for `${PWD}`
- Or use absolute path: `-v C:/Users/.../project/deploy/terraform:/workspace`

## Security Notes

- **Never commit tokens** to git (`.env` is gitignored)
- Rotate tokens every 90 days
- Use least-privilege scopes (e.g., `read:org` not `admin:org`)
- Each team member creates their own tokens
- Store production tokens in Azure Key Vault, not `.env`

## Support

- MCP specification: https://github.com/modelcontextprotocol
- GitHub MCP server: https://github.com/modelcontextprotocol/servers/tree/main/src/github
- Notion MCP server: https://github.com/modelcontextprotocol/servers/tree/main/src/notion
- Context7: https://github.com/context7/mcp-server
