---
applyTo: '**'
---

# CKAD environment: up / down via Copilot

When the user asks to **start / boot / up / launch** the CKAD practice environment, or **stop / down / tear down / delete** it, use the scripts in [`CKAD/scripts/`](../../CKAD/scripts/README.md). Do not invent commands; do not run `minikube start` manually.

## Intent - action mapping

| User says (any of) | Action |
|---|---|
| "start the env", "up the env", "boot ckad", "launch practice env", "start minikube for ckad" | **UP** (see below) |
| "stop the env", "down the env", "shut down ckad", "pause minikube" | **DOWN (stop)** |
| "delete the env", "nuke the cluster" | **DOWN (delete)** |
| "reset ckad", "fresh env", "clean slate" | **UP (clean slate)** - uses `-Reset` / `CKAD_RESET=1` |
| "open a new ckad shell", "give me a ckad terminal" | **UP from Windows** (opens a new WT tab) |

## UP

**Preferred (from this workspace on Windows):** run the PowerShell launcher. It opens a new Windows Terminal tab into WSL and boots everything.

```powershell
.\CKAD\scripts\Start-CKAD.ps1
```

For a clean slate (deletes and rebuilds the profile):

```powershell
.\CKAD\scripts\Start-CKAD.ps1 -Reset
```

**If the user is already in a WSL terminal**, instruct them to `source` (not execute) the up script so aliases and `KUBECONFIG`/namespace persist in their shell. Run from the repo root:

```bash
source CKAD/scripts/ckad-up.sh

# Clean slate:
CKAD_RESET=1 source CKAD/scripts/ckad-up.sh
```

Useful env-var overrides (set before the command):

| Variable | Default | Purpose |
|---|---|---|
| `CKAD_PROFILE` | `ckad` | minikube profile name |
| `CKAD_DRIVER` | `docker` | minikube driver |
| `CKAD_NS` | `practice` | namespace created and pinned as the kubectl default after UP |
| `CKAD_RESET` | `0` | set to `1` to delete the profile first (clean slate) |

> **Note:** After UP, the default namespace is pinned to `practice`. When running mock exam tasks, switch to the correct section namespace explicitly: `kubectl config set-context --current --namespace=ns-build` (or `ns-deploy`, `ns-config`, etc.).

## DOWN

From WSL:

```bash
# Stop (keeps cluster state - fast restart next time)
bash CKAD/scripts/ckad-down.sh

# Delete (wipes the profile - use when you want a clean slate next session)
CKAD_DELETE=1 bash CKAD/scripts/ckad-down.sh
```

## Rules for Copilot

### Script Execution

1. Always prefer the scripts above. Only fall back to raw `minikube`/`kubectl` if a script fails and the user asks to debug.
2. Do not run these scripts silently in the background. UP is interactive (new terminal tab); DOWN is a short foreground command.
3. Never call `minikube delete` directly - route it through `CKAD_DELETE=1 bash CKAD/scripts/ckad-down.sh` so the correct profile (`ckad`) is targeted.

### Docker Verification

4. Before running UP, verify Docker Desktop is running (`docker info` from WSL). If it is not, tell the user to start Docker Desktop first; do not attempt to start it from the shell.

### Post-Execution Checks

5. After UP, confirm with: `kubectl get nodes` (node should be `Ready`) and `kubectl config current-context` (should be `ckad`).
6. For prerequisites, one-time setup, and troubleshooting, direct the user to [CKAD/scripts/README.md](../../CKAD/scripts/README.md).
