# CKAD Environment Scripts

Minimal docs for booting and tearing down the CKAD practice environment.

| Script | Run from | Purpose |
|---|---|---|
| [`Start-CKAD.ps1`](Start-CKAD.ps1) | Windows PowerShell | Opens WSL in a new Windows Terminal tab and boots the env |
| [`ckad-up.sh`](ckad-up.sh) | WSL bash | Starts minikube, sets context + namespace, loads shell helpers |
| [`ckad-down.sh`](ckad-down.sh) | WSL bash | Stops (default) or deletes the minikube profile |

---

## Prerequisites (install once)

On **Windows**:

- [Windows Terminal](https://aka.ms/terminal) (gives you the `wt` command)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) with **WSL integration enabled** for your distro
- WSL2 + Ubuntu: `wsl --install -d Ubuntu`

Inside **WSL / Ubuntu**:

- `kubectl` — see [official install](https://kubernetes.io/docs/tasks/tools/install-kubectl-linux/)
- `minikube` — see [official install](https://minikube.sigs.k8s.io/docs/start/)
- (Optional) `bash-completion`: `sudo apt install -y bash-completion`

Verify:

```bash
kubectl version --client
minikube version
docker info
```

---

## One-time setup

### 1. Allow PowerShell scripts (Windows, once ever)

```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
```

### 2. Mark the bash scripts executable (WSL, once)

```bash
chmod +x /mnt/c/me/git/Kubernetes-practices/CKAD/scripts/*.sh
```

### 3. (Optional) Add WSL aliases for daily speed

Append to `~/.bashrc`:

```bash
alias ckad-up='source /mnt/c/me/git/Kubernetes-practices/CKAD/scripts/ckad-up.sh'
alias ckad-down='bash   /mnt/c/me/git/Kubernetes-practices/CKAD/scripts/ckad-down.sh'
```

Reload: `source ~/.bashrc`

---

## UP — start the environment

### From Windows (recommended)

```powershell
cd C:\me\git\Kubernetes-practices\CKAD\scripts
.\Start-CKAD.ps1
```

A new Windows Terminal tab titled **CKAD** opens running Ubuntu, sources `ckad-up.sh`, and leaves you in an interactive shell at `~`.

Variants:

| Goal | Command |
|---|---|
| Full clean slate (delete + rebuild profile) | `.\Start-CKAD.ps1 -Reset` |
| Use a specific WSL distro | `.\Start-CKAD.ps1 -Distro Ubuntu-24.04` |

### From inside an existing WSL shell

Use `source` (or `.`) so the shell helpers persist in the current shell:

```bash
source /mnt/c/me/git/Kubernetes-practices/CKAD/scripts/ckad-up.sh
# or, with the alias:
ckad-up
```

Clean slate:

```bash
CKAD_RESET=1 ckad-up
```

> Do **not** run the script with `bash ckad-up.sh`. A child shell exits immediately and your alias `k`, `$do`, `$now`, and tab-completion are lost.

### What `ckad-up.sh` does

1. Checks `minikube`, `kubectl`, `docker` are on PATH and Docker is running.
2. Starts (or resumes) the `ckad` minikube profile.
3. Sets kubectl context to `ckad` and pins default namespace to `practice`.
4. Loads `alias k=kubectl`, `$do`, `$now`, and bash completion for both `kubectl` and `k`.
5. Prints a summary of the state.

### Verify

```bash
kubectl config current-context                            # ckad
kubectl config view --minify -o jsonpath='{..namespace}'  # practice
k get nodes                                               # Ready
echo $do                                                  # --dry-run=client -o yaml
k get po<TAB>                                             # autocompletes
```

---

## DOWN — stop the environment

From any WSL shell:

| Goal | Command |
|---|---|
| Stop the cluster (keep state, fast restart) | `ckad-down` &nbsp; _or_ &nbsp; `bash .../ckad-down.sh` |
| Delete the cluster (full wipe, free disk) | `CKAD_DELETE=1 ckad-down` |

- **Stop** runs `minikube stop -p ckad`. Next `ckad-up` is near-instant.
- **Delete** runs `minikube delete -p ckad`. Use at end of a week or when you want a guaranteed clean slate.

---

## Environment variables (advanced)

Both scripts are configurable via env vars set before the call:

| Variable | Default | Used by | Effect |
|---|---|---|---|
| `CKAD_PROFILE` | `ckad` | up, down | Minikube profile name |
| `CKAD_DRIVER` | `docker` | up | Minikube driver |
| `CKAD_NS` | `practice` | up | Default namespace pinned in kubeconfig |
| `CKAD_RESET` | `0` | up | `1` = delete profile before starting |
| `CKAD_DELETE` | `0` | down | `1` = delete profile instead of stopping |

Example — two isolated profiles side by side:

```bash
CKAD_PROFILE=ckad-a CKAD_NS=practice ckad-up
CKAD_PROFILE=ckad-b CKAD_NS=staging  ckad-up
```

---

## Typical daily flow

```powershell
# 1. Morning (Windows)
cd C:\me\git\Kubernetes-practices\CKAD\scripts
.\Start-CKAD.ps1
```

```bash
# 2. In the new WSL tab — drills
cd ~
# … pick 5 drills from ../drills.md, timer on …

# 3. End of session — same tab
ckad-down          # stop (keep state for tomorrow)
```

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `Start-CKAD.ps1 : File … cannot be loaded` | Run the `Set-ExecutionPolicy` command in *One-time setup §1* |
| `wslpath translation failed` | `wsl -l -v` — confirm the distro name matches `-Distro` (case-sensitive) |
| `'docker' not found` or `Cannot connect to the Docker daemon` | Start Docker Desktop; enable WSL integration for your distro in Settings → Resources → WSL Integration |
| `k` alias / completion not working after `ckad-up` | You ran the script with `bash` instead of `source`. Re-run with `source` or use the `ckad-up` alias |
| minikube `kubelet` keeps crashing | `CKAD_DELETE=1 ckad-down` then `.\Start-CKAD.ps1 -Reset` |
| Slow file I/O when editing YAML | You are under `/mnt/c/...`. `cd ~` and work on the Linux filesystem — see [../linux-for-windows-users.md §2.3](../linux-for-windows-users.md#23-where-are-my-files) |

---

## Ask Copilot (hands-free up/down)

Copilot Chat is wired up via [.github/instructions/ckad-environment.instructions.md](../../.github/instructions/ckad-environment.instructions.md) to drive these scripts. Try any of:

| Prompt | What Copilot does |
|---|---|
| `start the ckad env` / `up the env` | Runs `Start-CKAD.ps1` (or `source ckad-up.sh` if you're already in WSL) |
| `stop the ckad env` / `down the env` | Runs `bash ckad-down.sh` |
| `reset the ckad env` / `nuke the cluster` | Runs `CKAD_DELETE=1 bash ckad-down.sh` then `Start-CKAD.ps1 -Reset` |
| `open a new ckad shell` | Runs `Start-CKAD.ps1` (new WT tab) |

Copilot will verify Docker Desktop is running before UP and will always target the `ckad` profile — it will never call raw `minikube delete`.

---

## See also

- [../README.md §5.4 One-command startup](../README.md#54-one-command-startup-windows--wsl) — brief mention in the main playbook.
- [../drills.md](../drills.md) — the 25 timed drills this env is for.
- [../linux-for-windows-users.md](../linux-for-windows-users.md) — WSL / bash primer for Windows users.
