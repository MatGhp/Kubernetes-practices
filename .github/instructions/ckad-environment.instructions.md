---
applyTo: '**'
---

# CKAD environment: up / down via Copilot

When the user asks to **start / boot / up / launch** the CKAD practice environment, or **stop / down / tear down / delete** it, use the scripts in [`CKAD/scripts/`](../../CKAD/scripts/README.md). Do not invent commands; do not run `minikube start` manually.

## Intent → action mapping

| User says (any of) | Action |
|---|---|
| "start the env", "up the env", "boot ckad", "launch practice env", "start minikube for ckad" | **UP** (see below) |
| "stop the env", "down the env", "shut down ckad", "pause minikube" | **DOWN (stop)** |
| "delete the env", "reset ckad", "nuke the cluster", "fresh env" | **DOWN (delete)** |
| "open a new ckad shell", "give me a ckad terminal" | **UP from Windows** (opens a new WT tab) |

## UP

**Preferred (from this workspace on Windows):** run the PowerShell launcher. It opens a new Windows Terminal tab into WSL and boots everything.

```powershell
pwsh -NoProfile -File .\CKAD\scripts\Start-CKAD.ps1
```

**If the user is already in a WSL terminal**, instruct them to `source` (not execute) the up script so aliases and `KUBECONFIG`/namespace persist in their shell:

```bash
source ./CKAD/scripts/ckad-up.sh
```

Useful env-var overrides (set before the command): `CKAD_PROFILE`, `CKAD_DRIVER`, `CKAD_NS`, `CKAD_RESET=1`.

## DOWN

From WSL:

```bash
# Stop (keeps cluster state — fast restart next time)
bash ./CKAD/scripts/ckad-down.sh

# Delete (wipes the profile — use for a clean slate)
CKAD_DELETE=1 bash ./CKAD/scripts/ckad-down.sh
```

## Rules for Copilot

1. Always prefer the scripts above. Only fall back to raw `minikube`/`kubectl` if a script fails and the user asks to debug.
2. Before running UP, verify Docker Desktop is running (`docker info`). If it is not, tell the user to start Docker Desktop first — do not attempt to start it from the shell.
3. Do not run these scripts silently in the background. UP is interactive (new terminal tab); DOWN is a short foreground command.
4. Never call `minikube delete` directly for this workspace — route it through `CKAD_DELETE=1 bash ./CKAD/scripts/ckad-down.sh` so the correct profile (`ckad`) is targeted.
5. After UP, a quick health check is: `kubectl get nodes` and `kubectl config current-context` (should be `ckad`).
6. Full reference lives in [CKAD/scripts/README.md](../../CKAD/scripts/README.md) — link to it instead of restating details.
