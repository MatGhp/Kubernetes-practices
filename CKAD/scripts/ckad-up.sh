#!/usr/bin/env bash
# Keep this file LF-only for WSL/bash compatibility.
# ckad-up.sh — Bring up the CKAD practice environment in one command.
# Safe to run repeatedly: idempotent. Run inside WSL/Ubuntu.
#
# What it does:
#   1. Starts the `ckad` Minikube profile (or creates it).
#   2. Switches kubectl context to `ckad`.
#   3. Ensures the `practice` namespace exists.
#   4. Pins the current context's default namespace to `practice`.
#   5. Exports the CKAD shell helpers ($do, $now, alias k, completion).
#   6. Drops you into an interactive bash shell in ~ with everything ready.
#
# Usage:
#   source scripts/ckad-up.sh       # preferred — keeps alias k, $do, $now, completion
#   bash   scripts/ckad-up.sh       # brings up the cluster only (helpers are lost on exit)
#
# Flags (env vars you can set before calling):
#   CKAD_DRIVER=docker              # minikube driver (default: docker)
#   CKAD_PROFILE=ckad               # minikube profile name (default: ckad)
#   CKAD_NS=practice                # target namespace (default: practice)
#   CKAD_RESET=1                    # delete the profile first (clean slate)

set -euo pipefail

export PATH="$HOME/.local/bin:$HOME/bin:$PATH"

CKAD_DRIVER="${CKAD_DRIVER:-docker}"
CKAD_PROFILE="${CKAD_PROFILE:-ckad}"
CKAD_NS="${CKAD_NS:-practice}"
CKAD_RESET="${CKAD_RESET:-0}"

log()  { printf '\033[1;34m[ckad-up]\033[0m %s\n' "$*"; }
ok()   { printf '\033[1;32m[ ok ]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[warn]\033[0m %s\n' "$*"; }
die()  { printf '\033[1;31m[fail]\033[0m %s\n' "$*" >&2; exit 1; }

# ---------------------------------------------------------------------------
# 1. Preconditions
# ---------------------------------------------------------------------------
for cmd in minikube kubectl docker; do
  command -v "$cmd" >/dev/null 2>&1 || die "'$cmd' not found in PATH. Install it first."
done

if ! docker info >/dev/null 2>&1; then
  die "Docker daemon not reachable. Start Docker Desktop (with WSL integration) and re-run."
fi

# ---------------------------------------------------------------------------
# 2. Optional clean slate
# ---------------------------------------------------------------------------
if [[ "$CKAD_RESET" == "1" ]]; then
  log "CKAD_RESET=1 — deleting profile '$CKAD_PROFILE'..."
  minikube delete -p "$CKAD_PROFILE" >/dev/null 2>&1 || true
fi

# ---------------------------------------------------------------------------
# 3. Start / ensure the minikube profile
# ---------------------------------------------------------------------------
if minikube status -p "$CKAD_PROFILE" 2>/dev/null | grep -q 'host: Running'; then
  ok "minikube profile '$CKAD_PROFILE' already running"
else
  log "starting minikube profile '$CKAD_PROFILE' (driver=$CKAD_DRIVER)..."
  minikube start -p "$CKAD_PROFILE" --driver="$CKAD_DRIVER"
fi

# ---------------------------------------------------------------------------
# 4. kubectl context + namespace
# ---------------------------------------------------------------------------
kubectl config use-context "$CKAD_PROFILE" >/dev/null
ok "context: $(kubectl config current-context)"

if ! kubectl get ns "$CKAD_NS" >/dev/null 2>&1; then
  log "creating namespace '$CKAD_NS'..."
  kubectl create namespace "$CKAD_NS" >/dev/null
fi
kubectl config set-context --current --namespace="$CKAD_NS" >/dev/null
ok "default namespace: $CKAD_NS"

# Sanity check
kubectl get nodes -o wide
echo
kubectl get ns

# ---------------------------------------------------------------------------
# 5. Shell helpers (alias + flags + completion)
#    These only stick if the script is *sourced* (see usage note above).
#    NOTE: `set -euo pipefail` from the top of this script leaks into the
#    interactive shell when sourced. bash-completion routinely references
#    unset vars and returns non-zero, which trips `-u` and `-e` and kills
#    the tab on <TAB>. Disable all three before installing helpers.
# ---------------------------------------------------------------------------
set +euo pipefail
alias k=kubectl
export do="--dry-run=client -o yaml"
export now="--grace-period=0 --force"
# shellcheck disable=SC1090
source <(kubectl completion bash)
complete -F __start_kubectl k 2>/dev/null || true
ok "shell helpers loaded (alias k, \$do, \$now, completion)"

# ---------------------------------------------------------------------------
# 6. Summary
# ---------------------------------------------------------------------------
cat <<EOF

CKAD environment is ready.
  Profile : $CKAD_PROFILE
  Driver  : $CKAD_DRIVER
  Context : $CKAD_PROFILE
  Default ns : $CKAD_NS

Next steps:
  k get all                                   # should be empty
  k create deployment web --image=nginx:1.27  # Drill 2
  cd ~ && vim pod.yaml                        # edit on Linux FS, not /mnt/c

Tear down later with:
  bash scripts/ckad-down.sh
EOF
