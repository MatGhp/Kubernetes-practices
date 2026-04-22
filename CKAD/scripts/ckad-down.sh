#!/usr/bin/env bash
# Keep this file LF-only for WSL/bash compatibility.
# ckad-down.sh — Tear down the CKAD practice environment.
# Run inside WSL/Ubuntu.
#
# Default: stops the minikube profile (fast; preserves data).
# Use CKAD_DELETE=1 to fully delete the profile (clean slate for next session).

set -euo pipefail

export PATH="$HOME/.local/bin:$HOME/bin:$PATH"

CKAD_PROFILE="${CKAD_PROFILE:-ckad}"
CKAD_DELETE="${CKAD_DELETE:-0}"

if [[ "$CKAD_DELETE" == "1" ]]; then
  echo "[ckad-down] deleting profile '$CKAD_PROFILE'..."
  minikube delete -p "$CKAD_PROFILE"
else
  echo "[ckad-down] stopping profile '$CKAD_PROFILE' (use CKAD_DELETE=1 to delete)..."
  minikube stop -p "$CKAD_PROFILE"
fi
