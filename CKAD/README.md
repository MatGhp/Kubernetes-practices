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
- [9. Local Practice vs Real Exam](#9-local-practice-vs-real-exam)
  - [9.1 Ingress (drills 31–32)](#91-ingress-drills-3132)
  - [9.2 NetworkPolicy (drill 23)](#92-networkpolicy-drill-23)
  - [9.3 PV / PVC / StorageClass (drill 25)](#93-pv--pvc--storageclass-drill-25)
  - [9.4 metrics-server / `kubectl top` (drill 33)](#94-metrics-server--kubectl-top-drill-33)
- [10. Exam-Day Runbook](#10-exam-day-runbook)
- [11. Pre-Exam Survival Checklist](#11-pre-exam-survival-checklist)

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
- **Exam machine** — the shell is fresh and nothing is preloaded. You paste a one-time warm-up block at the start of the exam. See [§10.1](#101-one-time-warm-up-run-once-when-the-exam-terminal-opens).

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

> Six drill sheets are available (study them in order):
> 1. [drills-1-core.md](drills-1-core.md) — 25 scenario-based drills (build a thing).
> 2. [drills-2-advanced.md](drills-2-advanced.md) — 12 advanced drills (SecurityContext, ServiceAccount, Ingress, Observability, multi-container, `kubectl edit`).
> 3. [drills-3-imperative.md](drills-3-imperative.md) — 32 verb-based drills that drill **every imperative `kubectl` command shape** the exam expects (speed multipliers).
> 4. [drills-4-modern.md](drills-4-modern.md) — 12 current-curriculum gap fillers (blue-green & canary, Helm, Kustomize, scheduling, `LimitRange`, `StorageClass`, CRD, PDB, `kubectl debug`).
> 5. [drills-5-mock-exam.md](drills-5-mock-exam.md) — 15-task, 2-hour timed simulation with self-scoring rubric. Take it last.
> 6. [drills-6-community.md](drills-6-community.md) — 12 gap-fillers inspired by the MIT-licensed `dgkanatsios/CKAD-exercises` (`kubectl explain`, downwardAPI, `subPath`, ExternalName, `rollout pause`, `kubectl cp`, etc.).

### Curriculum coverage matrix

Map of every published [CKAD curriculum](https://github.com/cncf/curriculum) competency to the drill files where it's exercised. Use this to find under-practiced areas before the exam.

| Domain (weight) | Competency | Files |
|-----------------|------------|-------|
| **App Design & Build (20%)** | Define, build, modify container images | 6 (Drill 50–51) |
| | Multi-container Pod patterns | 1 (D21), 2 (D35–36), 5 (T1), 6 (D52) |
| | Jobs and CronJobs | 1 (D18–19), 3 (imperative job/cronjob) |
| | Persistent and ephemeral volumes | 1 (D25), 4 (D46), 6 (D52–53) |
| **App Deployment (20%)** | Deployments and rolling updates | 1 (D2–4, 9), 3, 6 (D56–57) |
| | Deployment strategies (blue-green, canary) | 4 (D38–39), 5 (T4) |
| | Helm | 4 (D40), 5 (T5) |
| | Kustomize | 4 (D41), 5 (T6) |
| **Env, Config & Security (25%)** | ConfigMaps, Secrets | 1 (D11–15), 5 (T7) |
| | SecurityContext | 2 (D26–28), 5 (T8) |
| | ServiceAccount, Role/RoleBinding | 2 (D29–30), 5 (T9) |
| | Resource requirements & quotas | 1 (D17), 4 (D45) |
| | Pod scheduling (selectors, affinity) | 4 (D42–44) |
| | StorageClass / dynamic provisioning | 4 (D46) |
| | Discover & use CRDs | 4 (D47) |
| **Services & Networking (20%)** | Services & access | 1 (D5), 5 (T11–13), 6 (D54–55) |
| | Ingress | 2 (D31–32), 5 (T11) |
| | NetworkPolicies | 1 (D23), 3, 5 (T10) |
| **Observability & Maintenance (15%)** | Probes & health | 1 (D16), 5 (T3) |
| | Logs, events, metrics | 2 (D33–34), 6 (D60) |
| | Debug Pods, ephemeral containers | 1 (D20), 4 (D49), 5 (T14–15) |
| | `kubectl wait`, `cp`, port-forward | 5 (T13), 6 (D59, 61) |
| | PodDisruptionBudget | 4 (D48) |

If a row has only one file, that area is **lightly practiced** — make it your first weak-spot drill before booking the real exam.

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

## 9. Local Practice vs Real Exam

A few drills depend on Minikube-specific setup (CNI, addons, default StorageClass). The exam cluster is **not Minikube** — the local caveat goes away, but a different set of rules applies. The drills link here instead of repeating this content.

**General principle:** on the real exam the cluster is already provisioned. You don't `enable` addons, install controllers, pick a CNI, or create StorageClasses. You write the resource the task asks for, set the right namespace, and validate by behavior.

### 9.1 Ingress (drills 31–32)

> **Local env:** assumes the Minikube `ingress` addon is enabled. Resolve hosts via `$(minikube -p ckad ip)` or `/etc/hosts`.

**On the real exam:**

1. **An Ingress controller is already running** — don't deploy one. Confirm and move on:
   ```bash
   kubectl get ingressclass
   kubectl get pods -A | grep -iE 'ingress|nginx|traefik'
   ```
2. **Set `ingressClassName` explicitly** when the task names a class (or one is the cluster default). Without it the controller may silently ignore your Ingress.
3. **Scaffold imperatively, then edit** — saves typos in `pathType` and the nested `service.port` block:
   ```bash
   kubectl create ingress app -n <ns> \
     --rule="/=web:80" --rule="/api=api:80" \
     --class=nginx --dry-run=client -o yaml > ing.yaml
   # host-based variant:
   kubectl create ingress app --rule="web.local/*=web:80" --dry-run=client -o yaml
   ```
4. **`pathType` is mandatory** — admission rejects the object without it. Use `Prefix` unless told otherwise.
5. **`backend.service.port` must match the Service** — use `port.number` or `port.name`, whichever the Service exposes (`kubectl get svc <name> -o yaml`).
6. **No `minikube ip`, no `/etc/hosts` edits.** Validate from a throwaway pod or with curl headers:
   ```bash
   kubectl run tmp --rm -it --image=curlimages/curl --restart=Never -- \
     curl -s -H "Host: web.local" http://<ingress-controller-svc>.<ns>.svc/
   # or, if a NodePort/LoadBalancer is reachable:
   curl -s --resolve web.local:80:<addr> http://web.local/
   ```
7. **`ADDRESS` may stay empty** in `kubectl get ingress` — it is **not** part of grading. The grader inspects the spec.
8. **Watch the namespace.** Ingress and backend Services must live in the **same namespace**.
9. **TLS only if asked**, and only with a Secret the task provides — never generate certificates.
10. **Score on spec, then move on.** If `kubectl get ingress <name> -o yaml` shows the right class, rules, paths, `pathType`, and ports, you're done.

30-second fast-path:

```bash
kubectl get ingressclass                              # learn the class name
kubectl create ingress <name> --class=<class> \
  --rule="<host>/<path>=<svc>:<port>" \
  --dry-run=client -o yaml > ing.yaml
vim ing.yaml                                          # tweak pathType / add rules / TLS
kubectl apply -f ing.yaml
kubectl describe ingress <name> | grep -E 'Class|Host|Path|Backend'
```

### 9.2 NetworkPolicy (drill 23)

> **Local env:** Minikube needs a CNI that enforces NetworkPolicy — start with `minikube start -p ckad --cni=calico` to actually test enforcement.

**On the real exam:**

1. **The CNI already enforces policies** — don't try to install or change one.
2. **Read the namespace** — apply with `-n <ns>` and confirm with `kubectl get netpol -n <ns>`.
3. **Set `policyTypes` explicitly** (`Ingress`, `Egress`, or both). Omitting it means "infer from rules" — a common silent-fail when the question says "deny all egress".
4. **Default-deny is empty rules, not missing rules.** To deny all ingress for a selector use `policyTypes: [Ingress]` with **no** `ingress:` key (or `ingress: []`). Same for egress.
5. **Selector scoping matters.** `from.podSelector` matches pods in the **same namespace**; cross-namespace requires `namespaceSelector` (often combined with `podSelector`). Namespace labels matter — `kubectl label ns <ns> name=<ns>` if needed.
6. **DNS often needs an explicit egress rule** — UDP/TCP 53 to `kube-system` — when the task restricts egress and the pod resolves Service names.
7. **Validate by traffic, not by `describe`.** The grader checks behavior:
   ```bash
   kubectl run probe --rm -it --image=busybox --restart=Never --labels=role=client \
     -- wget -qO- --timeout=2 http://web.<ns>.svc.cluster.local || echo BLOCKED
   ```
8. **Don't restart pods.** Policies apply immediately to existing pods — recreating workloads wastes time.

### 9.3 PV / PVC / StorageClass (drill 25)

> **Local env:** Minikube ships with a default `standard` StorageClass (hostpath), so the PVC binds automatically without creating a PV.

**On the real exam** — behavior depends on the task. Two patterns are common; **read the question** to know which:

1. **"Create a PVC that binds."** A default StorageClass exists. Don't set `storageClassName` and the PVC will bind. Confirm:
   ```bash
   kubectl get sc                                       # find the (default) class
   kubectl get pvc <name> -o wide                       # STATUS should be Bound
   ```
2. **"Create a PV and a matching PVC."** No default StorageClass, OR the task asks you to back the claim with a specific PV (often `hostPath`). To force the binding to your PV, both sides must agree:
   - Use **`storageClassName: ""`** on PVC and PV to opt out of dynamic provisioning, **or** set the same explicit class name on both.
   - Match `accessModes` exactly and ensure `PV.capacity.storage >= PVC.requests.storage`.
   - Use a `selector` on the PVC if the task names labels for the PV.
3. **`ReadWriteOnce` vs `ReadWriteMany`** — only request RWX if the task says so; most exam clusters' default class only provides RWO and the PVC will hang in `Pending`.
4. **`Pending` PVC = read the events.** `kubectl describe pvc <name>` shows the exact mismatch (no class, no matching PV, wrong access mode, capacity too small).
5. **Reclaim policy** — only set `persistentVolumeReclaimPolicy: Retain` on the PV when the task explicitly requires data to survive PVC deletion.
6. **Verify across pods** with `kubectl exec` (write from one, read from another) — don't trust `kubectl get pvc` alone for round-trip tasks.

### 9.4 metrics-server / `kubectl top` (drill 33)

> **Local env:** the Minikube `metrics-server` addon must be enabled and warmed up before `kubectl top` works.

**On the real exam:**

1. **metrics-server is already installed and running.** You don't enable anything.
2. If `kubectl top` returns `Metrics API not available`, check the deployment is healthy:
   ```bash
   kubectl -n kube-system get deploy metrics-server
   ```
3. **Wait ~30 s** — metrics need a scrape interval before the first values appear, especially if pods just started.
4. **Don't troubleshoot it further** — it's not part of the question. Move on, return at the end if needed.

---

## 10. Exam-Day Runbook

### 10.1 One-time warm-up (run once when the exam terminal opens)

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

### 10.2 Per-task runbook

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

## 11. Pre-Exam Survival Checklist

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
