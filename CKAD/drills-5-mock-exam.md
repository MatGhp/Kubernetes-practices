# CKAD Mock Exam — 15 Tasks, 2 Hours

A timed simulation of the **current** CKAD exam format (April 2026). Original tasks — no leaked content. Designed to mirror the **shape** of the live exam: terminal-only, mixed difficulty, weighted percentages roughly matching the [published curriculum](https://github.com/cncf/curriculum), and worded to be unambiguous so you can self-grade.

> **Use this file last.** Run [drills-1-core.md](drills-1-core.md) → [drills-2-advanced.md](drills-2-advanced.md) → [drills-3-imperative.md](drills-3-imperative.md) → [drills-4-modern.md](drills-4-modern.md) → [drills-6-community.md](drills-6-community.md), then attempt this paper end-to-end.

## How to take it

1. **Pre-flight** (3 min, before you start the timer):

   ```bash
   bash ./CKAD/scripts/ckad-up.sh   # or pwsh -File .\CKAD\scripts\Start-CKAD.ps1
   kubectl get nodes                 # must be Ready
   kubectl create ns exam
   kubectl config set-context --current --namespace=exam
   alias k=kubectl
   export do="--dry-run=client -o yaml"
   ```
2. **Start a 120-minute timer** (the real exam is 2 hours).
3. Do tasks in order. Don't peek at answers. If a task takes more than its budget, **skip it** and come back.
4. At the end, expand the answers, run each `Verify` block, and score yourself.
5. **Pass mark: 66%** = score ≥ 66 of the 100 weighted points.

## Constraints (mirror the real exam)

- All tasks run in namespace **`exam`** unless stated otherwise.
- You may use any official Kubernetes documentation site (`kubernetes.io`, `helm.sh`, `kustomize.io`).
- **No** searching Stack Overflow, blogs, GitHub, AI assistants, or your own notes file. (Honour system here.)
- You may use `kubectl` aliases and the `$do` env var. The `k`, `kc`, `kn`, `kg` aliases from [README §2.2](README.md#22-kubectl-context--namespace-helpers) are fair game.

---

## Section 1 — Application Design and Build (20 pts)

### Task 1 — Multi-container Pod with shared `emptyDir` (6 pts, 5 min)

Create a Pod `task1` with **two containers**:

- `writer` (image `busybox:1.36`) running `sh -c "while true; do date >> /shared/dates.log; sleep 2; done"`
- `reader` (image `busybox:1.36`) running `sh -c "tail -f /shared/dates.log"`

Both must mount an `emptyDir` volume at `/shared`.

<details><summary>Answer</summary>

```yaml
# task1.yaml
apiVersion: v1
kind: Pod
metadata: { name: task1 }
spec:
  containers:
    - name: writer
      image: busybox:1.36
      command: ["sh","-c","while true; do date >> /shared/dates.log; sleep 2; done"]
      volumeMounts: [{ name: shared, mountPath: /shared }]
    - name: reader
      image: busybox:1.36
      command: ["sh","-c","tail -f /shared/dates.log"]
      volumeMounts: [{ name: shared, mountPath: /shared }]
  volumes:
    - name: shared
      emptyDir: {}
```

**Verify:**

```bash
kubectl apply -f task1.yaml
kubectl wait --for=condition=Ready pod/task1 --timeout=30s
kubectl logs task1 -c reader --tail=3
```
</details>

---

### Task 2 — Init container that gates startup (6 pts, 5 min)

Create a Pod `task2` with one main container `app` (`nginx:1.27`) and one init container `wait-for-svc` (`busybox:1.36`) that runs `sh -c "until nslookup gate.exam.svc.cluster.local; do sleep 2; done"`. Then create the Service `gate` (ClusterIP, port 80, no backing pods needed) and observe `task2` transition `Init` → `Running`.

<details><summary>Answer</summary>

```yaml
# task2.yaml
apiVersion: v1
kind: Service
metadata: { name: gate }
spec:
  selector: { app: nothing }   # no endpoints; DNS still resolves
  ports: [{ port: 80 }]
---
apiVersion: v1
kind: Pod
metadata: { name: task2 }
spec:
  initContainers:
    - name: wait-for-svc
      image: busybox:1.36
      command: ["sh","-c","until nslookup gate.exam.svc.cluster.local; do sleep 2; done"]
  containers:
    - name: app
      image: nginx:1.27
```

**Verify:**

```bash
kubectl apply -f task2.yaml
kubectl get pod task2 -w   # Ctrl-C once Running
```
</details>

---

### Task 3 — Build manifest from spec (8 pts, 6 min)

Create a Deployment `api` (3 replicas, image `nginx:1.27`) and a ClusterIP Service `api` on port 80. Add a `livenessProbe` (HTTP `GET /` on port 80, initial delay 5 s) and a `readinessProbe` (same target, period 5 s). Resource requests `cpu: 100m`, `memory: 64Mi`.

<details><summary>Answer</summary>

```bash
kubectl create deploy api --image=nginx:1.27 --replicas=3 $do > task3.yaml
# edit task3.yaml to add probes & resources, then:
kubectl apply -f task3.yaml
kubectl expose deploy api --port=80
```

```yaml
# extract from task3.yaml — container spec
        resources:
          requests: { cpu: 100m, memory: 64Mi }
        livenessProbe:
          httpGet: { path: /, port: 80 }
          initialDelaySeconds: 5
        readinessProbe:
          httpGet: { path: /, port: 80 }
          periodSeconds: 5
```

**Verify:**

```bash
kubectl get deploy api -o jsonpath='{.spec.template.spec.containers[0].livenessProbe}{"\n"}'
kubectl get svc api
```
</details>

---

## Section 2 — Application Deployment (20 pts)

### Task 4 — Canary at 10% (6 pts, 5 min)

A Service `shop` selects `app=shop` (no version label). Provide a `shop-stable` Deployment (9 replicas, `nginx:1.27`, labels `app=shop,track=stable`) and a `shop-canary` Deployment (1 replica, `nginx:1.28`, labels `app=shop,track=canary`).

<details><summary>Answer</summary>
See the pattern in [drills-4-modern.md Drill 39](drills-4-modern.md#drill-39--canary-9-stable--1-canary-behind-one-service).

**Verify:**

```bash
kubectl get endpoints shop -o jsonpath='{.subsets[*].addresses[*].ip}{"\n"}' | wc -w   # 10
```
</details>

---

### Task 5 — Helm release with override (6 pts, 5 min)

Add the `bitnami` Helm repo. Install chart `nginx` as release `front` with `service.type=ClusterIP` and `replicaCount=2` in this namespace.

<details><summary>Answer</summary>

```bash
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo update
helm install front bitnami/nginx \
  --set service.type=ClusterIP \
  --set replicaCount=2 \
  -n exam
```

**Verify:**

```bash
helm list -n exam
kubectl get deploy,svc -l app.kubernetes.io/instance=front
```
</details>

---

### Task 6 — Kustomize overlay (8 pts, 6 min)

Given a base Deployment `web` with 1 replica of `nginx:1.27`, write an overlay `staging/` that bumps to 4 replicas and adds `commonLabels: {env: staging}`. Apply with `kubectl apply -k`.

<details><summary>Answer</summary>
See [drills-4-modern.md Drill 41](drills-4-modern.md#drill-41--kustomize-overlay).

**Verify:**

```bash
kubectl get deploy web -o jsonpath='{.spec.replicas} {.metadata.labels.env}{"\n"}'   # 4 staging
```
</details>

---

## Section 3 — Environment, Configuration & Security (25 pts)

### Task 7 — ConfigMap + Secret consumption (6 pts, 4 min)

Create ConfigMap `app-cfg` with key `LOG_LEVEL=debug`. Create Secret `app-sec` with key `API_KEY=s3cr3t`. Run a Pod `task7` (`nginx:1.27`) that exposes `LOG_LEVEL` as an env var from the ConfigMap and mounts the Secret at `/etc/secrets`.

<details><summary>Answer</summary>

```bash
kubectl create cm  app-cfg --from-literal=LOG_LEVEL=debug
kubectl create secret generic app-sec --from-literal=API_KEY=s3cr3t
```

```yaml
# task7.yaml
apiVersion: v1
kind: Pod
metadata: { name: task7 }
spec:
  containers:
    - name: app
      image: nginx:1.27
      envFrom:
        - configMapRef: { name: app-cfg }
      volumeMounts:
        - { name: sec, mountPath: /etc/secrets, readOnly: true }
  volumes:
    - name: sec
      secret: { secretName: app-sec }
```

**Verify:**

```bash
kubectl exec task7 -- env | grep LOG_LEVEL
kubectl exec task7 -- ls /etc/secrets
```
</details>

---

### Task 8 — SecurityContext: non-root + read-only FS (7 pts, 5 min)

Pod `task8` (image `nginx:1.27`) must run as user `1001`, group `2002`, with a **read-only root filesystem** and an `emptyDir` mounted at `/var/cache/nginx` and `/var/run` so nginx can still start.

<details><summary>Answer</summary>

```yaml
# task8.yaml
apiVersion: v1
kind: Pod
metadata: { name: task8 }
spec:
  securityContext:
    runAsUser: 1001
    runAsGroup: 2002
    fsGroup: 2002
  containers:
    - name: nginx
      image: nginx:1.27
      securityContext:
        readOnlyRootFilesystem: true
        allowPrivilegeEscalation: false
      volumeMounts:
        - { name: cache, mountPath: /var/cache/nginx }
        - { name: run,   mountPath: /var/run }
  volumes:
    - { name: cache, emptyDir: {} }
    - { name: run,   emptyDir: {} }
```

**Verify:**

```bash
kubectl exec task8 -- id     # uid=1001 gid=2002
kubectl exec task8 -- touch /test 2>&1 | grep -i "read-only"
```
</details>

---

### Task 9 — ServiceAccount + read-only Role (6 pts, 5 min)

Create ServiceAccount `viewer`, Role `pod-viewer` allowing `get,list,watch` on `pods`, and a RoleBinding `viewer-binds-pod-viewer` binding the SA to the Role. Mount the SA in a Pod `task9` (`bitnami/kubectl:latest`) and run `kubectl get pods` from inside.

<details><summary>Answer</summary>

```bash
kubectl create sa viewer
kubectl create role pod-viewer --verb=get,list,watch --resource=pods
kubectl create rolebinding viewer-binds-pod-viewer \
  --role=pod-viewer --serviceaccount=exam:viewer
```

```yaml
# task9.yaml
apiVersion: v1
kind: Pod
metadata: { name: task9 }
spec:
  serviceAccountName: viewer
  containers:
    - { name: kc, image: "bitnami/kubectl:latest", command: ["sleep","3600"] }
```

**Verify:**

```bash
kubectl exec task9 -- kubectl get pods -n exam
kubectl exec task9 -- kubectl get deploy -n exam 2>&1 | grep -i forbidden
```
</details>

---

### Task 10 — NetworkPolicy: default-deny + allow from one label (6 pts, 5 min)

Add two NetworkPolicies in namespace `exam`:

1. `default-deny` denying all ingress.
2. `allow-frontend` allowing ingress to pods with `app=backend` from pods with `app=frontend` on TCP 80.

<details><summary>Answer</summary>

```yaml
# task10.yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata: { name: default-deny }
spec:
  podSelector: {}
  policyTypes: [Ingress]
---
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata: { name: allow-frontend }
spec:
  podSelector: { matchLabels: { app: backend } }
  policyTypes: [Ingress]
  ingress:
    - from:
        - podSelector: { matchLabels: { app: frontend } }
      ports:
        - { protocol: TCP, port: 80 }
```

**Verify:**

```bash
kubectl get netpol -n exam
```
</details>

---

## Section 4 — Services and Networking (20 pts)

### Task 11 — Path-based Ingress (8 pts, 6 min)

Two Deployments (`web-a`, `web-b`, both `nginx:1.27`, 1 replica) and matching ClusterIP Services. Create Ingress `paths` so `/a` → `web-a`, `/b` → `web-b`. Use `pathType: Prefix`. Assume the cluster's default IngressClass.

<details><summary>Answer</summary>
See [drills-2-advanced.md Drill 31](drills-2-advanced.md#drill-31--path-based-ingress).

**Verify:**

```bash
kubectl get ingress paths
kubectl describe ingress paths | grep -A3 "Host\|Path"
```
</details>

---

### Task 12 — Headless Service for stable DNS (6 pts, 4 min)

Create a headless Service (`clusterIP: None`) `db` on port 5432 selecting `app=db`. Then a 2-replica Deployment `db` (`postgres:16`, env `POSTGRES_PASSWORD=x`). Demonstrate that DNS returns **both** Pod IPs.

<details><summary>Answer</summary>

```yaml
# task12.yaml
apiVersion: v1
kind: Service
metadata: { name: db }
spec:
  clusterIP: None
  selector: { app: db }
  ports: [{ port: 5432, targetPort: 5432 }]
---
apiVersion: apps/v1
kind: Deployment
metadata: { name: db }
spec:
  replicas: 2
  selector: { matchLabels: { app: db } }
  template:
    metadata: { labels: { app: db } }
    spec:
      containers:
        - name: pg
          image: postgres:16
          env: [{ name: POSTGRES_PASSWORD, value: x }]
```

**Verify:**

```bash
kubectl run dns --rm -it --restart=Never --image=busybox:1.36 -- \
  nslookup db.exam.svc.cluster.local
# Expect TWO addresses listed.
```
</details>

---

### Task 13 — `port-forward` to a private Service (6 pts, 3 min)

Forward local port 9090 to Service `api` (port 80) in namespace `exam`, then `curl` `http://localhost:9090` to confirm a 200 response. Run the forward in the background and clean it up.

<details><summary>Answer</summary>

```bash
kubectl port-forward svc/api 9090:80 -n exam &
PF=$!
sleep 1
curl -sI http://localhost:9090 | head -1   # HTTP/1.1 200 OK
kill $PF
```
</details>

---

## Section 5 — Observability & Maintenance (15 pts)

### Task 14 — Diagnose a CrashLooping Pod (8 pts, 7 min)

Apply this manifest as-is, then **fix** it without deleting and recreating the Pod (use `kubectl edit`):

```yaml
apiVersion: v1
kind: Pod
metadata: { name: task14 }
spec:
  containers:
    - name: app
      image: busybox:1.36
      command: ["/bin/false"]   # the bug
```

<details><summary>Answer</summary>

```bash
kubectl apply -f - <<EOF
apiVersion: v1
kind: Pod
metadata: { name: task14 }
spec:
  containers:
    - name: app
      image: busybox:1.36
      command: ["/bin/false"]
EOF

# Diagnose
kubectl get pod task14
kubectl describe pod task14 | grep -A2 "Last State\|Reason\|Exit Code"
kubectl logs task14 --previous

# Fix in-place — Pod's `spec.containers[*].command` is mutable for non-running fields? No: most fields are immutable.
# `kubectl edit pod task14` will reject. The expected workflow:
kubectl get pod task14 -o yaml > task14.yaml
# fix command to ["sleep","3600"] in the file
kubectl replace --force -f task14.yaml
```

**Note:** The exam expects you to recognise that Pod `command` is immutable — `replace --force` (or delete+apply) is the correct fix. Full credit for either.

**Verify:**

```bash
kubectl wait --for=condition=Ready pod/task14 --timeout=30s
```
</details>

---

### Task 15 — Ephemeral debug container (7 pts, 5 min)

A running Pod `target` (image `nginx:1.27`) has no shell debugging tools. Without restarting it, attach an ephemeral `busybox:1.36` container, `wget` `localhost`, and capture the response status into a file `/tmp/probe.txt` on your local host.

<details><summary>Answer</summary>

```bash
kubectl run target --image=nginx:1.27
kubectl wait --for=condition=Ready pod/target --timeout=30s

kubectl debug -it target --image=busybox:1.36 --target=target -- \
  sh -c "wget -qSO- http://localhost 2>&1 | head -5" \
  > /tmp/probe.txt
cat /tmp/probe.txt
```

**Verify:** the file contains `HTTP/1.1 200 OK`.
</details>

---

## Self-scoring rubric

After your 120 minutes are up:

| Section | Tasks | Max pts |
|---------|-------|---------|
| Application Design & Build | 1, 2, 3 | 20 |
| Application Deployment | 4, 5, 6 | 20 |
| Env, Config & Security | 7, 8, 9, 10 | 25 |
| Services & Networking | 11, 12, 13 | 20 |
| Observability & Maintenance | 14, 15 | 15 |
| **Total** | | **100** |

For each task, award:

- **Full points** if every `Verify` command produces the expected result *without* you peeking at the answer block.
- **Half points** if you peeked at the answer once or your verify required minor manual fix-ups.
- **Zero** if you skipped or the verify failed.

**Pass mark: 66 / 100.** If you scored less, identify the weakest section, drill the corresponding source file (1–4 or 6), and re-attempt this paper after a 24-hour gap.

## Cleanup

```bash
kubectl delete ns exam
helm uninstall front -n exam 2>/dev/null
```

---

## Why this is exam-shaped (not exam content)

Every task above is **original**. Each one targets a well-known curriculum bullet from [cncf/curriculum](https://github.com/cncf/curriculum) using the same *style* the live exam uses (build a thing, verify it works, move on). No leaked content was used, referenced, or paraphrased.
