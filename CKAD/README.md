# CKAD Exam Preparation

A clean, exam-focused playbook for the **Certified Kubernetes Application Developer (CKAD)** exam.

The exam rewards three things:

1. **Speed** with `kubectl` (imperative first, YAML second).
2. **Clean edits** in `vim` on a Linux shell.
3. **Fast debugging** of broken workloads.

Everything in this guide is shaped around those three.

---

## Table of Contents

- [1. Environment Setup](#1-environment-setup)
  - [1.1 Minikube profile](#11-minikube-profile-dedicated-to-ckad)
  - [1.2 Windows users — WSL2 + Linux + vim](#12-windows-users--wsl2--linux--vim)
- [2. Shell Productivity Setup](#2-shell-productivity-setup)
  - [2.1 The block](#21-the-block)
  - [2.2 Permanent setup on WSL / Ubuntu (practice machine)](#22-permanent-setup-on-wsl--ubuntu-practice-machine)
  - [2.3 vim config for YAML](#23-vim-config-for-yaml)
- [3. Skills That Actually Matter](#3-skills-that-actually-matter)
  - [3.1 Linux shell essentials](#31-linux-shell-essentials)
  - [3.2 vim essentials](#32-vim-essentials)
  - [3.3 kubectl essentials](#33-kubectl-essentials)
- [4. Speed Patterns](#4-speed-patterns)
  - [4.1 Imperative first, YAML when needed](#41-imperative-first-yaml-when-needed)
  - [4.2 Use kubectl explain as your docs](#42-use-kubectl-explain-as-your-docs)
  - [4.3 Filter output fast](#43-filter-output-fast)
- [5. Practice System](#5-practice-system)
  - [5.1 Three-phase method](#51-three-phase-method)
  - [5.2 60-minute drill format](#52-60-minute-drill-format)
  - [5.3 Minikube reset loop](#53-minikube-reset-loop)
  - [5.4 One-command startup (Windows → WSL)](#54-one-command-startup-windows--wsl)
- [6. Core CKAD Drills](#6-core-ckad-drills)
- [7. Debugging Playbook](#7-debugging-playbook)
- [8. Common Pitfalls](#8-common-pitfalls)
- [9. Exam-Day Runbook](#9-exam-day-runbook)
- [10. Pre-Exam Survival Checklist](#10-pre-exam-survival-checklist)

---

## 1. Environment Setup

### 1.1 Minikube profile dedicated to CKAD

Use a dedicated profile so practice is isolated and easy to reset.

```bash
# Create the profile
minikube start -p ckad --driver=docker

# Point kubectl to it
kubectl config use-context ckad

# Verify
kubectl get nodes
kubectl create namespace practice
```

> Keep the same driver across sessions so performance stays predictable.

### 1.2 Windows users — WSL2 + Linux + vim

The exam runs on Linux with terminal editing (typically `vim`). The goal for Windows users is to remove hesitation on shell + vim.

Recommended setup:

- **WSL2** + **Ubuntu**
- `kubectl`
- **Minikube** (driver: `docker`)
- **vim**

Run all CKAD practice inside WSL — not PowerShell. This makes:

- commands behave like exam conditions,
- `vim` native and fast,
- Linux muscle memory build naturally.

---

## 2. Shell Productivity Setup

Set these up so every drill is faster. There are **two scenarios**:

- **Practice machine (WSL/Ubuntu)** — install permanently in `~/.bashrc`. Do this once.
- **Exam machine** — the shell is fresh and nothing is preloaded. You paste a one-time warm-up block at the start of the exam. See [§9.1](#91-one-time-warm-up-run-once-when-the-exam-terminal-opens).

Both scenarios use the same content.

### 2.1 The block

```bash
# Shorter alias
alias k=kubectl

# Common flags
export do="--dry-run=client -o yaml"   # generate YAML
export now="--grace-period=0 --force"  # force delete

# Autocomplete for kubectl AND k
source <(kubectl completion bash)
complete -F __start_kubectl k
```

Usage:

```bash
k run nginx --image=nginx $do > nginx.yaml
k delete pod nginx $now
```

### 2.2 Permanent setup on WSL / Ubuntu (practice machine)

**Step 1 — install `bash-completion`** (Ubuntu auto-sources it from `~/.bashrc`):

```bash
sudo apt update && sudo apt install -y bash-completion
```

**Step 2 — append the block to `~/.bashrc`** with a **quoted heredoc** so `$do` and `$(...)` are written literally, not expanded. Copy and run this in your terminal exactly as-is (the closing `EOF` must sit at column 1):

```bash
cat >> ~/.bashrc <<'EOF'

# ---- CKAD kubectl setup ----
alias k=kubectl
export do="--dry-run=client -o yaml"
export now="--grace-period=0 --force"
source <(kubectl completion bash)
complete -F __start_kubectl k
# ---- /CKAD kubectl setup ----
EOF
```

**Step 3 — reload the shell:**

```bash
source ~/.bashrc
```

**Step 4 — verify:**

```bash
k version --client     # alias works
echo $do               # prints: --dry-run=client -o yaml
k get po<TAB>          # completes to: k get pods
```

> Notes
> - Put it in `~/.bashrc`, **not** `~/.bash_profile` — default Ubuntu does not source the latter.
> - Run the `cat >>` block **once**. If you run it again, first remove the marked section (`# ---- CKAD kubectl setup ----`) from `~/.bashrc`.
> - zsh users: write the block into `~/.zshrc`, replace `kubectl completion bash` with `kubectl completion zsh`, and prepend `autoload -U compinit && compinit`.

### 2.3 vim config for YAML

Create `~/.vimrc`:

```vim
set number
set expandtab
set tabstop=2
set shiftwidth=2
set autoindent
set pastetoggle=<F2>
syntax on
```

This prevents the most common exam-time YAML indentation failures.

> Do **not** use `set paste` permanently. Paste mode disables `autoindent` and `expandtab` during typing, defeating the other settings. Instead, use `pastetoggle=<F2>`: press `F2` before pasting a multi-line block, paste, then press `F2` again to resume normal editing.

---

## 3. Skills That Actually Matter

A minimal subset is enough. Drill only these until they are automatic.

### 3.1 Linux shell essentials

**Navigation:** `pwd`, `ls`, `cd`, `mkdir`, `rm`, `cat`, `cp`, `mv`

**Redirects & pipes:** `>`, `>>`, `|`

**Text tools:** `grep`, `less`, `head`, `tail`, `wc`, `sort`, `uniq`

Examples:

```bash
kubectl get pod mypod -o yaml > pod.yaml
cat pod.yaml | grep image
kubectl get pods -A | grep CrashLoopBackOff
```

> Coming from Windows? See [linux-for-windows-users.md](linux-for-windows-users.md) for a Windows→Linux mental model, a PowerShell↔bash cheat sheet, and a 15-minute warm-up drill.

### 3.2 vim essentials

Open:

```bash
vim file.yaml
```

| Action | Keys |
|---|---|
| Insert mode | `i` |
| Leave insert mode | `Esc` |
| Save | `:w` |
| Quit | `:q` |
| Save and quit | `:wq` |
| Quit without saving | `:q!` |
| Top / bottom of file | `gg` / `G` |
| Search | `/text` then `n` for next |
| Copy line | `yy` |
| Delete line | `dd` |
| Paste below | `p` |
| Undo / Redo | `u` / `Ctrl+r` |

This is all you need for CKAD.

### 3.3 kubectl essentials

Memorize these commands cold:

- `kubectl run`, `kubectl create`, `kubectl apply -f`, `kubectl delete -f`
- `kubectl get`, `kubectl describe`, `kubectl logs`, `kubectl exec -it`
- `kubectl scale`, `kubectl set image`, `kubectl rollout undo`
- `kubectl expose`, `kubectl port-forward`
- `kubectl explain`
- `kubectl config use-context`, `kubectl config set-context --current --namespace=<ns>`

---

## 4. Speed Patterns

### 4.1 Imperative first, YAML when needed

For simple tasks, skip YAML entirely:

```bash
kubectl create deployment web --image=nginx -n practice
kubectl expose deployment web --port=80 --target-port=80 -n practice
kubectl scale deployment web --replicas=3 -n practice
```

For anything complex, **generate YAML from an imperative command** and edit:

```bash
kubectl create deployment web --image=nginx $do > web.yaml
vim web.yaml
kubectl apply -f web.yaml
```

This is typically faster than writing YAML from scratch.

Common generators:

```bash
kubectl run web --image=nginx $do > pod.yaml
kubectl create deployment web --image=nginx $do > deploy.yaml
kubectl create service clusterip web --tcp=80:80 $do > svc.yaml
kubectl create configmap app-cfg --from-literal=ENV=prod $do > cm.yaml
kubectl create secret generic app-sec --from-literal=KEY=abc $do > sec.yaml
kubectl create job hello --image=busybox $do -- echo hi > job.yaml
kubectl create cronjob hello --image=busybox --schedule="*/5 * * * *" $do -- echo hi > cron.yaml
```

### 4.2 Use kubectl explain as your docs

Faster than browsing the website during the exam:

```bash
kubectl explain pod.spec.containers
kubectl explain deployment.spec.strategy
kubectl explain pod.spec.containers.resources --recursive
```

### 4.3 Filter output fast

```bash
# All pods everywhere, only failing ones
kubectl get pods -A | grep -E 'CrashLoopBackOff|Error|ImagePullBackOff'

# Extract a single field with JSONPath
kubectl get pod mypod -o jsonpath='{.status.phase}'

# Custom columns
kubectl get pods -o custom-columns=NAME:.metadata.name,NODE:.spec.nodeName

# Watch something change
kubectl get pods -w
```

---

## 5. Practice System

### 5.1 Three-phase method

1. **Accuracy (no timer)** — solve each task correctly from memory.
2. **Soft timer** — 10–15 minutes per task.
3. **Mixed drill** — 5 tasks back-to-back using only official Kubernetes docs and `kubectl --help` / `kubectl explain`.

### 5.2 60-minute drill format

- **10 min** — cluster reset + 2 warm-up tasks
- **35 min** — 3 medium tasks (deployments, config, probes)
- **10 min** — 1 debugging task (broken pod or service)
- **5 min** — log commands you fumbled

Track misses in a simple file (`task`, `mistake`, `fix`, `faster command`).

### 5.3 Minikube reset loop

Run before a drill set so every session starts clean:

```bash
minikube delete -p ckad
minikube start -p ckad --driver=docker
kubectl config use-context ckad
kubectl create namespace practice
kubectl config set-context --current --namespace=practice
```

Setting the default namespace once saves typing `-n practice` on every command.

### 5.4 One-command startup (Windows → WSL)

Two scripts automate everything in §1 + §2 + §5.3 so every practice session starts in under 10 seconds. **Full docs:** [scripts/README.md](scripts/README.md).

- **[scripts/Start-CKAD.ps1](scripts/Start-CKAD.ps1)** — Windows launcher. Opens Windows Terminal → WSL Ubuntu and sources the bootstrap below. Pin it to your taskbar or run from PowerShell:

  ```powershell
  cd C:\me\git\Kubernetes-practices\CKAD
  .\scripts\Start-CKAD.ps1           # boot the env
  .\scripts\Start-CKAD.ps1 -Reset    # full clean-slate (deletes the profile first)
  ```

- **[scripts/ckad-up.sh](scripts/ckad-up.sh)** — WSL bootstrap. Idempotent. Can also be run directly inside an existing WSL shell:

  ```bash
  source CKAD/scripts/ckad-up.sh            # preferred: keeps alias k / $do / $now in the shell
  CKAD_RESET=1 source CKAD/scripts/ckad-up.sh  # clean slate
  ```

- **[scripts/ckad-down.sh](scripts/ckad-down.sh)** — stop (default) or delete (`CKAD_DELETE=1`) the minikube profile at the end of a session.

What `ckad-up.sh` does:

1. Verifies `minikube`, `kubectl`, `docker` are on PATH and the Docker daemon is reachable.
2. Starts (or creates) the `ckad` minikube profile.
3. Switches kubectl context to `ckad` and pins default namespace to `practice`.
4. Loads the §2.1 shell helpers (alias `k`, `$do`, `$now`, bash completion).
5. Prints a summary and leaves you in an interactive shell at `~`.

---

## 6. Core CKAD Drills

Run these repeatedly until they feel automatic.

### Core workloads

1. Create a namespace `practice`.
2. Create a deployment `web` using `nginx`.
3. Scale `web` to 3 replicas.
4. Update `web` to a different `nginx` tag.
5. Expose `web` internally on port 80.
6. Create a pod `tools` using `busybox` that sleeps forever.
7. Exec into `tools` and run `wget` against the service.
8. Print the labels of the `web` pods.
9. Rollout undo the last deployment change.
10. Generate a deployment YAML via `--dry-run=client -o yaml`, edit it in vim, apply.

### Config and secrets

11. Create a ConfigMap from literals.
12. Inject ConfigMap values into a pod as env vars.
13. Mount a ConfigMap as a volume.
14. Create a Secret from literals.
15. Consume the Secret in a pod (env or volume).

### Probes, resources, Jobs

16. Add `readinessProbe` and `livenessProbe` to a pod.
17. Set CPU and memory `requests` and `limits`.
18. Create a Job that runs once and completes.
19. Create a CronJob that runs every 5 minutes.
20. Debug a failing pod using `describe` + `logs`.

### Multi-container and networking

21. Add a **sidecar** container that tails a shared log file.
22. Add an **initContainer** that waits for a dependency.
23. Create a NetworkPolicy that allows traffic only from pods with a specific label.
24. Use `port-forward` to access a pod locally.

### Storage

25. Create a PVC, mount it in a pod, write data, delete the pod, confirm data persists.

---

## 7. Debugging Playbook

When something breaks, walk this path in order:

```bash
# 1. Status of everything
kubectl get all -n practice

# 2. Why is the pod not Ready?
kubectl describe pod <pod> -n practice

# 3. What did the container say?
kubectl logs <pod> -n practice
kubectl logs <pod> -c <container> -n practice      # multi-container
kubectl logs <pod> --previous -n practice          # after a crash

# 4. Go inside the pod
kubectl exec -it <pod> -n practice -- sh

# 5. Recent cluster events
kubectl get events -n practice --sort-by=.metadata.creationTimestamp
```

Key diagnostic signals:

| Symptom | First thing to check |
|---|---|
| `ImagePullBackOff` | Image name/tag, registry auth, typo |
| `CrashLoopBackOff` | `logs --previous`, command/args, probes too strict |
| Pod `Pending` | `describe` events: resources, PVC binding, nodeSelector, taints |
| Service not reachable | Service `selector` vs pod labels, `targetPort`, pod `Ready` |
| ConfigMap/Secret not applied | Pod must be restarted; check volume vs env mode |

---

## 8. Common Pitfalls

- **Forgot the namespace** — set it once per session: `kubectl config set-context --current --namespace=practice`.
- **Wrong context** — before anything destructive: `kubectl config current-context`.
- **Indentation in YAML** — use the `vim` config in [2.3](#23-vim-config-for-yaml); always `kubectl apply --dry-run=client -f file.yaml` before the real apply.
- **Writing YAML from scratch** — almost always slower than generating with `--dry-run=client -o yaml`.
- **Editing live objects** — prefer `kubectl edit <resource>` over recreating when the task says “modify”.
- **Slow deletes** — use `$now` (`--grace-period=0 --force`) when the task says to remove fast.
- **Service selector mismatch** — check `kubectl get pods --show-labels` vs `kubectl describe svc`.
- **Probes failing** — check path, port, and `initialDelaySeconds`; probe must match what the app actually exposes.

---

## 9. Exam-Day Runbook

### 9.1 One-time warm-up (run once when the exam terminal opens)

The exam shell is fresh — aliases and completion are **not** preloaded, and nothing you set in one task's shell carries over automatically. Paste the [§2.1 block](#21-the-block) at the very start:

```bash
alias k=kubectl
export do="--dry-run=client -o yaml"
export now="--grace-period=0 --force"
source <(kubectl completion bash)
complete -F __start_kubectl k
```

Tips for speed on the exam machine:

- Keep the block in a **text file on your local machine** so you can paste it in one keystroke; do not re-type it.
- If the exam terminal tab is replaced (new shell), paste the block again — it is cheap.
- Open the allowed docs tab (`kubernetes.io/docs`) **before** starting the clock-sensitive work.

### 9.2 Per-task runbook

Do these at the start of **every** task:

1. Read the task twice. Note the **namespace** and **context** required.
2. Switch context:
   ```bash
   kubectl config use-context <given-context>
   ```
3. Set the namespace if one is given (removes the need to type `-n` on every command):
   ```bash
   kubectl config set-context --current --namespace=<given-ns>
   ```
4. Sanity check before anything destructive:
   ```bash
   kubectl config current-context
   kubectl get pods
   ```
5. Attempt **imperatively** first.
6. If not possible imperatively, generate YAML:
   ```bash
   kubectl ... $do > q.yaml
   vim q.yaml
   kubectl apply -f q.yaml
   ```
7. Verify with `kubectl get` + `kubectl describe` / `logs`.
8. Flag and move on if stuck more than ~1.5× the time budget for that task.

Time discipline matters more than any single question.

---

## 10. Pre-Exam Survival Checklist

You should be able to do all of these without thinking.

**vim**

- `vim file.yaml`, `i`, `Esc`, `:wq`, `:q!`
- `/image`, `n`, `yy`, `dd`, `p`, `u`

**kubectl**

- `kubectl config use-context <ctx>`
- `kubectl config set-context --current --namespace=<ns>`
- `kubectl get pods -A`
- `kubectl describe pod <pod>`
- `kubectl logs <pod>` (plus `--previous`, `-c <container>`)
- `kubectl exec -it <pod> -- sh`
- `kubectl create deployment web --image=nginx $do > web.yaml`
- `kubectl apply -f web.yaml`
- `kubectl rollout undo deployment/web`
- `kubectl explain pod.spec.containers`

If any of those costs more than a few seconds — drill it until it does not.
