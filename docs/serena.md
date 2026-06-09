# Serena — LSP-powered code intelligence

Serena is an MCP (Model Context Protocol) server that gives Claude Code
language-server–level code intelligence: find declarations, find references,
rename symbols, insert/replace code, and get diagnostics — all via LSP rather
than text matching.

It runs in Docker alongside a local `.NET SDK` + `fsautocomplete` install so the
F# LSP can type-check and navigate the backend.

## Architecture

```
┌──────────────────────────────────────────────────┐
│  Claude Code (host)                               │
│  .mcp.json → http://localhost:9121/sse            │
└──────────────┬───────────────────────────────────┘
               │ SSE
┌──────────────▼───────────────────────────────────┐
│  Docker container: serena-fsharp                  │
│  ┌──────────────────────────────────────────────┐│
│  │  Serena MCP server (Python, port 9121)        ││
│  │  └─ LSP backend                              ││
│  │       ├─ fsautocomplete (F#)                  ││
│  │       └─ dart-language-server                ││
│  │  Dashboard: port 24282                        ││
│  └──────────────────────────────────────────────┘│
│  /workspace/osaHealth ← mounted from repo root   │
└──────────────────────────────────────────────────┘
```

## Files involved

| File | Role |
|---|---|
| `.mcp.json` | Tells Claude Code about the Serena SSE endpoint |
| `serena/compose.yaml` | Docker Compose service definition |
| `serena/Dockerfile` | Extends `serena:latest` with .NET SDK + fsautocomplete |
| `serena/entrypoint.sh` | `dotnet restore` then launch Serena |
| `serena/config/serena_config.yml` | Global Serena config (projects list, modes, LSP backend) |
| `.serena/project.yml` | Per-project config (name, languages, ignore patterns) |

## Setup (one-time)

### 1. Build the base Serena image

From the [Serena repository](https://github.com/oraios/serena) root:

```bash
docker build -t serena:latest .
```

### 2. Build and start the F#-extended container

```bash
cd osaHealth/serena
docker compose up --build serena-fsharp
```

### 3. Verify

Once the container is running, activate the project in Claude Code:

```
Find the declaration of registerOpenApi
```

Serena should respond with the symbol's location and body. If you get
`-32602` errors, run `/mcp` to reconnect.

## Configuration

Claude Code reads the project local `.mcp.json` on startup. The `serena` key provides the endpoint for the SSE transport.

### `serena/config/serena_config.yml`

Key settings:

- `projects: ["/workspace/osaHealth"]` — registered project path (inside
  the container filesystem)
- `language_backend: LSP` — uses fsautocomplete/dart-ls via SolidLSP
- `base_modes: [interactive, editing]` — enables symbol editing tools
- `web_dashboard: true` — dashboard at `http://localhost:24282`

### `.serena/project.yml`

Generated on first activation. Defines:

- `project_name: osaHealth`
- `languages: [fsharp, dart]` — determines which language servers start
- `ignored_paths: [node_modules, .git, backlog]`
- `encoding: utf-8`

## Available tools

Serena exposes 23 tools when connected via Claude Code:
* <https://oraios.github.io/serena/01-about/035_tools.html>

## Usage patterns

### Find a symbol's definition

Uses `find_declaration` with `include_body: true` to return both location
and source.

```fsharp
 Find the declaration of registerOpenApi                                      
  ⎿  ⧉ Selected 1 lines from ..\..\..\..\..\..\mnt\c\Users\kami\workspace\githu
     b-space\osaHealth\src\osaHealth.Api\Program.fs in JetBrains Rider
  
  Thought for 17s, called serena (ctrl+o to expand)

● registerOpenApi — src/osaHealth.Api/Program.fs:10-17, inside module OpenApi:

  registerOpenApi (app: WebApplication) : WebApplication =
      // ...

  WebApplication -> WebApplication — registers Swagger UI assets and serves the
  OpenAPI spec at /openapi/v1.json. Called from main at line 77 during
  middleware pipeline setup.
```

### Explore a file

Uses `get_symbols_overview` — returns symbol kinds and name paths without
reading the whole file.

```
Get an overview of Program.fs                                                                                                  

  Thought for 17s, called serena (ctrl+o to expand)

● src/osaHealth.Api/Program.fs symbol overview:
     
  ┌────────┬─────────────────┬──────────────────────────────────────────────────┐
  │  Kind  │      Name       │                   Description                    │
  ├────────┼─────────────────┼──────────────────────────────────────────────────┤
  │ Module │ OpenApi         │ Explicit module (line 10)                        │
  ├────────┼─────────────────┼──────────────────────────────────────────────────┤
  │ Module │ Program         │ Implicit top-level module                        │
  ├────────┼─────────────────┼──────────────────────────────────────────────────┤
  │ Field  │ registerOpenApi │ WebApplication -> WebApplication, inside OpenApi │
  ├────────┼─────────────────┼──────────────────────────────────────────────────┤
  │ Field  │ randomHandler   │ EndpointHandler — top-level handler binding      │
  ├────────┼─────────────────┼──────────────────────────────────────────────────┤
  │ Field  │ endpoints       │ Top-level Oxpecker route list                    │
  ├────────┼─────────────────┼──────────────────────────────────────────────────┤
  │ Field  │ main            │ string[] -> int — entry point                    │
  └────────┴─────────────────┴──────────────────────────────────────────────────┘

```

### Check for errors

Returns LSP errors/warnings grouped by symbol and severity.

```
Get diagnostics for Program.fs                                                                                                 

  Thought for 11s, called serena (ctrl+o to expand)

● No errors or warnings — only 5 hints (FSAC0004: redundant parentheses):
     
  ┌─────────────────┬──────┬────────────────────────────┐
  │     Symbol      │ Line │         Suggestion         │
  ├─────────────────┼──────┼────────────────────────────┤
  │ registerOpenApi │ 12   │ Parentheses can be removed │
  ├─────────────────┼──────┼────────────────────────────┤
  │ randomHandler   │ 21   │ Parentheses can be removed │
  ├─────────────────┼──────┼────────────────────────────┤                                           
  │ endpoints       │ 33   │ Parentheses can be removed │
  ├─────────────────┼──────┼────────────────────────────┤
  │ endpoints       │ 46   │ Parentheses can be removed │
  ├─────────────────┼──────┼────────────────────────────┤
  │ main            │ 58   │ Parentheses can be removed │
  └─────────────────┴──────┴────────────────────────────┘

  These are all the same low-severity style hint from fsautocomplete — the code is clean.

```

### Rename a symbol

Uses `rename_symbol` — updates the definition and all references across
the codebase.

```
❯ Rename randomHandler to randomEndpointHandler                                
  ⎿  ⧉ Selected 1 lines from ..\..\..\..\..\..\mnt\c\Users\kami\workspace\githu
     b-space\osaHealth\src\osaHealth.Api\Program.fs in JetBrains Rider
  
  Thought for 14s, read 1 file, called serena (ctrl+o to expand)

● 1 change applied. Let me verify the reference was updated too:
     
  Thought for 14s, searched for 1 pattern, read 1 file, called MCP (ctrl+o to 
  expand)

● Both definition and reference updated cleanly — no stale randomHandler left
  behind. Let me confirm no diagnostics regressed:
     
  Thought for 12s, read 1 file, called serena (ctrl+o to expand)
     
  Rename summary:
  - Definition: Program.fs:20 — let randomEndpointHandler : EndpointHandler =
  - Reference: Program.fs:44 — route "/random" randomEndpointHandler
  - Build: clean

```

### Search for a symbol

Searches by name path pattern (supports partial matching, absolute paths
like `/module/function`, and depth for child symbols).

```fs
❯ Find the symbol main                                                                                                           

  Thought for 14s, called serena 2 times (ctrl+o to expand)

● Hit an .fsx file the LSP can't parse. Restricting to the API project:
 
  Thought for 8s, called serena (ctrl+o to expand)

● main — Program.fs:57-83, entry point (string[] -> int):

  main args =
      let builder = WebApplication.CreateBuilder(args)
      // ...      
      let app = builder.Build()    
      // ...
      app.Run()
      0

  Builds the host, wires JSON console logging, registers Oxpecker + OpenAPI, configures the middleware pipeline, runs the app,
  returns exit code 0.

```

## Troubleshooting

### "Couldn't find file in LoadedProjects"

The F# LSP (`fsautocomplete`) needs time to load the project after
restore. The project graph build takes 2–5 seconds. If you call a tool
immediately after `activate_project`, it may fail with this error.

**Fix:** Wait a few seconds after activation, then retry. Look for
`fsharp/notifyWorkspace` messages in Serena's logs (they signal the
project has loaded).

### Build FAILED — FSharp.Core not found

**Root cause:** The `.fsproj` pins a preview version of `FSharp.Core`
that isn't on the NuGet feed inside the container.

**Fix:** Remove the explicit `PackageReference Update="FSharp.Core"`
from the `.fsproj`. The SDK ships with a matching FSharp.Core — the
implicit reference is sufficient.

### AppleDouble `._*` files cause YAML parse errors

**Root cause:** Building `serena:latest` on macOS creates AppleDouble
metadata files alongside real config files. Serena's YAML scanner
chokes on their binary content.

**Fix:** The `Dockerfile` already strips them at build time:
`RUN find /workspaces/serena -name '._*' -delete`

### MCP calls fail with `-32602`

The MCP connection is stale (e.g., after container restart).

**Fix:** Run `/mcp` in Claude Code to reconnect.

### Dashboard

Open `http://localhost:24282/dashboard/` in a browser to see:
- Active tools and modes
- Language server status
- Session logs
- Configuration

## Rebuilding after config changes

```bash
cd serena
docker compose up --build serena-fsharp
```

If only the `.fsproj` or source code changed, a restart is enough:

```bash
docker compose restart serena-fsharp
```

The `entrypoint.sh` runs `dotnet restore` on every container start, so new
dependencies are picked up automatically.
