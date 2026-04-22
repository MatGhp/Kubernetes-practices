#!/usr/bin/env bash
# ckad-bashrc.sh — Interactive bash init file used by Start-CKAD.ps1.
# Launched via `bash --rcfile`, so this replaces normal ~/.bashrc loading.
# We first re-source the user's ~/.bashrc (to keep their prompt, PATH, aliases),
# then source ckad-up.sh so the CKAD helpers (alias k, $do, $now, completion)
# are defined in THIS interactive shell.

# 1. User's normal interactive setup.
if [ -f "$HOME/.bashrc" ]; then
  # shellcheck disable=SC1091
  source "$HOME/.bashrc"
fi

# 2. CKAD bootstrap (idempotent; safe to re-source).
_ckad_script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$_ckad_script_dir/ckad-up.sh"
unset _ckad_script_dir
