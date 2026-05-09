# CKAD Mock Exam — 18 Tasks, ~2.5 Hours

A timed simulation of the **current** CKAD exam format (April 2026). Original tasks — no leaked content. Designed to mirror the **shape** of the live exam: terminal-only, mixed difficulty, weighted percentages roughly matching the [published curriculum](https://github.com/cncf/curriculum), and worded to be unambiguous so you can self-grade.

> **Use this file last.** Run [drills-1-core.md](drills-1-core.md) → [drills-2-advanced.md](drills-2-advanced.md) → [drills-3-imperative.md](drills-3-imperative.md) → [drills-4-modern.md](drills-4-modern.md) → [drills-6-community.md](drills-6-community.md), then attempt this paper end-to-end.

## How to take it

1. **Pre-flight** (3 min, before you start the timer):

   ```bash
   bash ./CKAD/scripts/ckad-up.sh   # or pwsh -File .\CKAD\scripts\Start-CKAD.ps1
   kubectl get nodes                 # must be Ready
   for ns in ns-build ns-deploy ns-config ns-network ns-observe; do
     kubectl create ns $ns 2>/dev/null
   done
   alias k=kubectl
   export do="--dry-run=client -o yaml"
   # Switch namespace at the start of each section:
   #   kubectl config set-context --current --namespace=<ns>
   ```
2. **Start a 150-minute timer** (the real exam is 2 hours; this extended paper has 3 additional tasks).
3. Do tasks in order. Don't peek at answers. If a task takes more than its budget, **skip it** and come back.
4. At the end, expand the answers, run each `Verify` block, and score yourself.
5. **Pass mark: 66%** = score ≥ 79 of the 120 weighted points.

## Constraints (mirror the real exam)

- Each section runs in its own namespace (see section header). Switch with `kubectl config set-context --current --namespace=<ns>` at the start of each section.
- You may use any official Kubernetes documentation site (`kubernetes.io`, `helm.sh`, `kustomize.io`).
- **No** searching Stack Overflow, blogs, GitHub, AI assistants, or your own notes file. (Honour system here.)
- You may use `kubectl` aliases and the `$do` env var. The `k`, `kc`, `kn`, `kg` aliases from [README §2.2](README.md#22-kubectl-context--namespace-helpers) are fair game.

---

## Section 1 — Application Design and Build (27 pts)

> **Namespace:** `ns-build` — `kubectl config set-context --current --namespace=ns-build`

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

Create a Pod `task2` with one main container `app` (`nginx:1.27`) and one init container `wait-for-svc` (`busybox:1.36`) that runs `sh -c "until nslookup gate.ns-build.svc.cluster.local; do sleep 2; done"`. Then create the Service `gate` (ClusterIP, port 80, no backing pods needed) and observe `task2` transition `Init` → `Running`.

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
      command: ["sh","-c","until nslookup gate.ns-build.svc.cluster.local; do sleep 2; done"]
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

> **Namespace:** `ns-deploy` — `kubectl config set-context --current --namespace=ns-deploy`

### Task 4 — Canary at 10% (6 pts, 5 min)

A Service `shop` selects `app=shop` (no version label). Create a `shop-stable` Deployment (9 replicas, `nginx:1.27`, labels `app=shop,track=stable`) and a `shop-canary` Deployment (1 replica, `nginx:1.28`, labels `app=shop,track=canary`).

<details><summary>Answer</summary>

```yaml
# task4.yaml
apiVersion: v1
kind: Service
metadata:
  name: shop
  namespace: ns-deploy
spec:
  selector:
    app: shop          # matches both deployments; no version/track label
  ports:
    - port: 80
      targetPort: 80
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: shop-stable
  namespace: ns-deploy
spec:
  replicas: 9
  selector:
    matchLabels: { app: shop, track: stable }
  template:
    metadata:
      labels: { app: shop, track: stable }
    spec:
      containers:
        - name: nginx
          image: nginx:1.27
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: shop-canary
  namespace: ns-deploy
spec:
  replicas: 1
  selector:
    matchLabels: { app: shop, track: canary }
  template:
    metadata:
      labels: { app: shop, track: canary }
    spec:
      containers:
        - name: nginx
          image: nginx:1.28
```

```bash
kubectl apply -f task4.yaml
```

**Verify:**

```bash
# EndpointSlice (non-deprecated, v1.33+)
kubectl get endpointslices -l kubernetes.io/service-name=shop \
  -o go-template='{{range .items}}{{range .endpoints}}{{range .addresses}}{{.}}{{"\n"}}{{end}}{{end}}{{end}}' | wc -l   # 10
```
</details>

---

### Task 5 — Helm release with override (6 pts, 5 min)

In namespace `ns-deploy`, the Helm repo `bitnami` (`https://charts.bitnami.com/bitnami`) has been added for you. Install chart `bitnami/nginx` as release `front` with `replicaCount=2` and `service.type=ClusterIP`.

> **Exam note:** Real CKAD Helm tasks always provide the repo URL (or pre-add the repo) and the exact chart name and `--set` values. You never have to remember third-party URLs.

<details><summary>Answer</summary>

```bash
# Repo is already added on the exam; included here so the drill is self-contained.
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo update

helm install front bitnami/nginx \
  --set replicaCount=2 \
  --set service.type=ClusterIP \
  -n ns-deploy
```

**Verify:**

```bash
helm list -n ns-deploy
kubectl get deploy,svc -l app.kubernetes.io/instance=front
```
</details>

---

### Task 6 — Kustomize overlay (8 pts, 6 min)

A base is **already provided** at `~/task6/base/` containing a Deployment `web` (1 replica of `nginx:1.27`) and a `kustomization.yaml` listing it as a resource.

Create an overlay at `~/task6/overlays/staging/` that, when applied with `kubectl apply -k ~/task6/overlays/staging -n ns-deploy`, produces a Deployment `web` with **4 replicas** and label `env=staging`. Do not modify any file under `base/`.

> **Setup (run once before the timer — simulates the pre-created files the real exam gives you):**
>
> ```bash
> mkdir -p ~/task6/base ~/task6/overlays/staging
>
> cat > ~/task6/base/deployment.yaml <<'EOF'
> apiVersion: apps/v1
> kind: Deployment
> metadata: { name: web }
> spec:
>   replicas: 1
>   selector: { matchLabels: { app: web } }
>   template:
>     metadata: { labels: { app: web } }
>     spec:
>       containers:
>         - { name: nginx, image: nginx:1.27 }
> EOF
>
> cat > ~/task6/base/kustomization.yaml <<'EOF'
> resources: [deployment.yaml]
> EOF
> ```

<details><summary>Answer</summary>

You only author two files under `~/task6/overlays/staging/`:

```yaml
# ~/task6/overlays/staging/kustomization.yaml
resources: [../../base]
labels:
  - pairs: { env: staging }
    includeSelectors: false
patches:
  - path: replica-patch.yaml
    target: { kind: Deployment, name: web }
```

```yaml
# ~/task6/overlays/staging/replica-patch.yaml
apiVersion: apps/v1
kind: Deployment
metadata: { name: web }
spec:
  replicas: 4
```

```bash
kubectl apply -k ~/task6/overlays/staging -n ns-deploy
```

> **Note:** `commonLabels` is deprecated in newer Kustomize; use `labels:` with `includeSelectors: false` so the new label isn't injected into the immutable `selector.matchLabels`.

**Verify:**

```bash
kubectl get deploy web -n ns-deploy -o jsonpath='{.spec.replicas} {.metadata.labels.env}{"\n"}'   # 4 staging
```
</details>

---

## Section 3 — Environment, Configuration & Security (25 pts)

> **Namespace:** `ns-config` — `kubectl config set-context --current --namespace=ns-config`

### Task 7 — ConfigMap + Secret consumption (6 pts, 4 min)

Create ConfigMap `app-cfg` with key `LOG_LEVEL=debug`. Create Secret `app-sec` with key `API_KEY=s3cr3t`. Create a Pod `task7` (`nginx:1.27`) that exposes `LOG_LEVEL` as an env var from the ConfigMap and mounts the Secret at `/etc/secrets`.

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
    fsGroup: 2002       # sets owning group on mounted volumes
  containers:
    - name: nginx
      image: nginx:1.27
      securityContext:
        readOnlyRootFilesystem: true        # MUST be container-level, not pod-level (invalid in pod level)
        allowPrivilegeEscalation: false
      volumeMounts:
        - { name: cache, mountPath: /var/cache/nginx }
        - { name: run,   mountPath: /var/run }
  volumes:
    - { name: cache, emptyDir: {} }   # separate volumes, not one shared emptyDir
    - { name: run,   emptyDir: {} }
```

> **Common mistakes to avoid:**
>
> 1. **`readOnlyRootFilesystem` at pod level** — this field belongs under `spec.containers[*].securityContext`, not `spec.securityContext`. Placing it at pod level is silently ignored; the filesystem stays writable.
> 2. **Missing `fsGroup`** — without it, mounted volumes are owned by root. The process can still write because emptyDir defaults to mode `0777`, but the task explicitly requires group `2002`.
> 3. **One emptyDir for both mounts** — Kubernetes allows it, but sharing one volume across two mount paths creates hidden coupling. Use two separate `emptyDir` volumes.

**Verify:**

```bash
kubectl exec task8 -- id     # uid=1001 gid=2002
kubectl exec task8 -- touch /test 2>&1 | grep -i "read-only"
```
</details>

---

### Task 9 — ServiceAccount + read-only Role (6 pts, 5 min)

Create ServiceAccount `viewer`, Role `pod-viewer` allowing `get,list,watch` on `pods`, and a RoleBinding `viewer-binds-pod-viewer` binding the SA to the Role. Create a Pod `task9` (`bitnami/kubectl:latest`) using the `viewer` ServiceAccount. Confirm the SA can list pods in namespace `ns-config` but **cannot** list Deployments (expect `Forbidden`).

<details><summary>Answer</summary>

```bash
kubectl create sa viewer
kubectl create role pod-viewer --verb=get,list,watch --resource=pods
kubectl create rolebinding viewer-binds-pod-viewer \
  --role=pod-viewer --serviceaccount=ns-config:viewer
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
kubectl exec task9 -- kubectl get pods                              # succeeds — SA has pod-viewer Role
kubectl exec task9 -- kubectl get deploy 2>&1 | grep -i forbidden   # denied — Deployments not in Role
```
</details>

---

### Task 10 — NetworkPolicy: default-deny + allow from one label (6 pts, 5 min)

Add two NetworkPolicies in namespace `ns-config`:

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
kubectl get netpol -n ns-config
```
</details>

---

## Section 4 — Services and Networking (20 pts)

> **Namespace:** `ns-network` — `kubectl config set-context --current --namespace=ns-network`

### Task 11 — Path-based Ingress (8 pts, 6 min)

In namespace `ns-network`, two Deployments (`web-a`, `web-b`, both `nginx:1.27`, 1 replica) and matching ClusterIP Services on port 80 **already exist**. Create an Ingress named `paths` so that:

- `/a` → Service `web-a` on port 80
- `/b` → Service `web-b` on port 80

Use `pathType: Prefix` and the cluster's default IngressClass.

> **Setup (run once before the timer — simulates the pre-created Deployments and Services):**
>
> ```bash
> kubectl -n ns-network create deploy web-a --image=nginx:1.27
> kubectl -n ns-network create deploy web-b --image=nginx:1.27
> kubectl -n ns-network expose deploy web-a --port=80
> kubectl -n ns-network expose deploy web-b --port=80
> ```

<details><summary>Answer</summary>

```yaml
# task11.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: paths
  namespace: ns-network
spec:
  rules:
    - http:
        paths:
          - path: /a
            pathType: Prefix
            backend:
              service:
                name: web-a
                port: { number: 80 }
          - path: /b
            pathType: Prefix
            backend:
              service:
                name: web-b
                port: { number: 80 }
```

```bash
kubectl apply -f task11.yaml
```

**Verify:**

```bash
kubectl get ingress paths -n ns-network
kubectl describe ingress paths -n ns-network | grep -E 'Path|Backend'
```
</details>

---

### Task 12 — Headless Service for stable DNS (6 pts, 4 min)

Create a headless Service (`clusterIP: None`) `db` on port 5432 selecting `app=db`. Create a 2-replica Deployment `db` (`postgres:16`, env `POSTGRES_PASSWORD=x`). Confirm that an `nslookup` of `db.ns-network.svc.cluster.local` returns **two** IP addresses.

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
  nslookup db.ns-network.svc.cluster.local
# Expect TWO addresses listed.
```
</details>

---

### Task 13 — `port-forward` to a private Service (6 pts, 3 min)

Forward local port 9090 to Service `api` (port 80) in namespace `ns-network`, then `curl` `http://localhost:9090` to confirm a 200 response. Run the forward in the background and clean it up.

> **Setup (run once before the timer — simulates the pre-created `api` Service):**
>
> ```bash
> kubectl -n ns-network create deploy api --image=nginx:1.27
> kubectl -n ns-network expose deploy api --port=80
> kubectl -n ns-network wait --for=condition=Available deploy/api --timeout=30s
> ```

<details><summary>Answer</summary>

```bash
kubectl port-forward svc/api 9090:80 -n ns-network &
PF=$!
sleep 1
curl -sI http://localhost:9090 | head -1   # HTTP/1.1 200 OK
kill $PF
```
</details>

---

## Section 5 — Observability & Maintenance (28 pts)

> **Namespace:** `ns-observe` — `kubectl config set-context --current --namespace=ns-observe`

### Task 14 — Diagnose a CrashLooping Pod (8 pts, 7 min)

Apply this manifest as-is, diagnose the failure, then fix it so the Pod reaches `Running`:

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

# Fix — Pod's `spec.containers[*].command` is immutable, so `kubectl edit` will reject the change.
# Recreate via replace --force (or delete + apply):
kubectl get pod task14 -o yaml > task14.yaml
# edit task14.yaml: change command to ["sleep","3600"]
kubectl replace --force -f task14.yaml
```

**Note:** The exam expects you to recognise that Pod `command` is immutable. `replace --force` and `delete` + `apply` both earn full credit.

**Verify:**

```bash
kubectl wait --for=condition=Ready pod/task14 --timeout=30s
```
</details>

---

### Task 15 — Ephemeral debug container (7 pts, 5 min)

A running Pod `target` (image `nginx:1.27`) has no shell debugging tools. Without restarting it, attach an ephemeral `busybox:1.36` container, `wget` `localhost`, and capture the response status into a file `/tmp/probe.txt` on your local host.

> **Setup (run once before the timer — simulates the running Pod the real exam gives you):**
>
> ```bash
> kubectl run target --image=nginx:1.27 -n ns-observe
> kubectl wait --for=condition=Ready pod/target -n ns-observe --timeout=30s
> ```

<details><summary>Answer</summary>

```bash
kubectl debug -it target -n ns-observe --image=busybox:1.36 --target=target -- \
  sh -c "wget -qSO- http://localhost 2>&1 | head -5" \
  > /tmp/probe.txt
cat /tmp/probe.txt
```

**Verify:** the file contains `HTTP/1.1 200 OK`.
</details>

---

### Task 16 — Build and deploy a local container image (7 pts, 6 min)

> **Curriculum:** Application Design & Build — container images  
> **Namespace:** `ns-build` (switch back if needed)

Use minikube's Docker daemon to build and deploy a custom image **without a registry**:
1. Create a `Dockerfile` (base: `nginx:1.27`) that copies a local `index.html` containing `Hello CKAD` to `/usr/share/nginx/html/index.html`.
2. Build the image as `my-app:v1` inside minikube's Docker daemon (`eval $(minikube docker-env -p ckad)`).
3. Run a Pod `task16` (namespace `ns-build`) using that image with `imagePullPolicy: Never`.
4. Verify `curl localhost` from inside the Pod prints `Hello CKAD`.

<details><summary>Answer</summary>

```bash
eval $(minikube docker-env -p ckad)

echo 'Hello CKAD' > index.html

cat > Dockerfile <<'EOF'
FROM nginx:1.27
COPY index.html /usr/share/nginx/html/index.html
EXPOSE 80
EOF

docker build -t my-app:v1 .
```

```yaml
# task16.yaml
apiVersion: v1
kind: Pod
metadata:
  name: task16
  namespace: ns-build
spec:
  containers:
    - name: app
      image: my-app:v1
      imagePullPolicy: Never
      ports:
        - containerPort: 80
```

```bash
kubectl apply -f task16.yaml
kubectl wait --for=condition=Ready pod/task16 -n ns-build --timeout=30s
kubectl exec -n ns-build task16 -- curl -s localhost   # Hello CKAD
```

**Why `imagePullPolicy: Never`?** The image lives only inside minikube's Docker daemon — there is no registry. `Never` tells the kubelet to use the locally cached image instead of attempting a remote pull (which would fail with `ErrImagePull`).
</details>

**Cleanup:** `kubectl delete pod task16 -n ns-build; docker rmi my-app:v1; eval $(minikube docker-env -u -p ckad)`

---

### Task 17 — Identify and fix a deprecated API version (5 pts, 4 min)

> **Curriculum:** Application Observability & Maintenance — API deprecations  
> **Namespace:** `ns-observe`

Apply the manifest below, note the error, find the correct `apiVersion` using only `kubectl`, and re-apply a corrected version.

```yaml
apiVersion: batch/v1beta1
kind: CronJob
metadata:
  name: cleanup
spec:
  schedule: "*/5 * * * *"
  jobTemplate:
    spec:
      template:
        spec:
          containers:
            - name: task
              image: busybox:1.36
              command: ["sh", "-c", "echo cleaning"]
          restartPolicy: OnFailure
```

<details><summary>Answer</summary>

```bash
# Step 1 — apply and observe the error
kubectl apply -n ns-observe -f - <<'EOF'
apiVersion: batch/v1beta1
kind: CronJob
metadata: { name: cleanup }
spec:
  schedule: "*/5 * * * *"
  jobTemplate:
    spec:
      template:
        spec:
          containers: [{ name: task, image: busybox:1.36, command: ["sh","-c","echo cleaning"] }]
          restartPolicy: OnFailure
EOF
# Error: no matches for kind "CronJob" in version "batch/v1beta1"
# batch/v1beta1 was removed in Kubernetes 1.25.

# Step 2 — find the correct version without a browser
kubectl api-resources --api-group=batch
# NAME       SHORTNAMES   APIVERSION   NAMESPACED   KIND
# cronjobs   cj           batch/v1     true         CronJob

# Step 3 — apply corrected manifest
kubectl apply -n ns-observe -f - <<'EOF'
apiVersion: batch/v1
kind: CronJob
metadata: { name: cleanup }
spec:
  schedule: "*/5 * * * *"
  jobTemplate:
    spec:
      template:
        spec:
          containers: [{ name: task, image: busybox:1.36, command: ["sh","-c","echo cleaning"] }]
          restartPolicy: OnFailure
EOF
```

**Verify:**

```bash
kubectl get cronjob cleanup -n ns-observe
```

**Common API removals to memorise:**

| Old version | Replacement | Removed in |
|---|---|---|
| `batch/v1beta1` CronJob | `batch/v1` | 1.25 |
| `networking.k8s.io/v1beta1` Ingress | `networking.k8s.io/v1` | 1.22 |
| `policy/v1beta1` PodDisruptionBudget | `policy/v1` | 1.25 |
</details>

**Cleanup:** `kubectl delete cronjob cleanup -n ns-observe`

---

### Task 18 — Debug and fix a broken Deployment (8 pts, 7 min)

> **Curriculum:** Application Observability & Maintenance — debugging workloads  
> **Namespace:** `ns-observe`

Apply this Deployment. It has **two bugs** — find both and fix them so all 2 replicas reach `Running`.

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: task18
spec:
  replicas: 2
  selector:
    matchLabels: { app: task18 }
  template:
    metadata:
      labels: { app: task18 }
    spec:
      containers:
        - name: app
          image: nginx:doesnotexist
          resources:
            requests: { cpu: "500m", memory: "128Mi" }
            limits:   { cpu: "100m", memory: "64Mi" }
```

<details><summary>Answer</summary>

```bash
kubectl apply -n ns-observe -f - <<'EOF'
apiVersion: apps/v1
kind: Deployment
metadata: { name: task18 }
spec:
  replicas: 2
  selector: { matchLabels: { app: task18 } }
  template:
    metadata: { labels: { app: task18 } }
    spec:
      containers:
        - name: app
          image: nginx:doesnotexist
          resources:
            requests: { cpu: "500m", memory: "128Mi" }
            limits:   { cpu: "100m", memory: "64Mi" }
EOF

# Diagnose
kubectl get pod -n ns-observe -l app=task18
kubectl describe pod -n ns-observe -l app=task18 | grep -A5 "State\|Reason\|Events"
```

**Bug 1 — bad image tag:** `nginx:doesnotexist` → pods enter `ErrImagePull` / `ImagePullBackOff`. Fix:

```bash
kubectl set image deployment/task18 app=nginx:1.27 -n ns-observe
```

**Bug 2 — cpu limit < request:** `limits.cpu: 100m` is less than `requests.cpu: 500m`. Kubernetes rejects pods with this constraint. Fix with `kubectl edit`:

```bash
kubectl edit deployment task18 -n ns-observe
# change limits.cpu from "100m" to "1"
```

Then watch the rollout complete:

```bash
kubectl rollout status deployment/task18 -n ns-observe
```

**Verify:**

```bash
kubectl get deploy task18 -n ns-observe   # READY: 2/2
```
</details>

**Cleanup:** `kubectl delete deploy task18 -n ns-observe`

---

## Self-scoring rubric

After your 150 minutes are up:

| Section | Tasks | Max pts |
|---------|-------|---------|
| Application Design & Build | 1, 2, 3, 16 | 27 |
| Application Deployment | 4, 5, 6 | 20 |
| Env, Config & Security | 7, 8, 9, 10 | 25 |
| Services & Networking | 11, 12, 13 | 20 |
| Observability & Maintenance | 14, 15, 17, 18 | 28 |
| **Total** | | **120** |

For each task, award:

- **Full points** if every `Verify` command produces the expected result *without* you peeking at the answer block.
- **Half points** if you peeked at the answer once or your verify required minor manual fix-ups.
- **Zero** if you skipped or the verify failed.

**Pass mark: 79 / 120.** If you scored less, identify the weakest section, drill the corresponding source file (1–4 or 6), and re-attempt this paper after a 24-hour gap.

## Cleanup

```bash
kubectl delete ns ns-build ns-deploy ns-config ns-network ns-observe
helm uninstall front -n ns-deploy 2>/dev/null
```

---

## Why this is exam-shaped (not exam content)

Every task above is **original**. Each one targets a well-known curriculum bullet from [cncf/curriculum](https://github.com/cncf/curriculum) using the same *style* the live exam uses (build a thing, verify it works, move on). No leaked content was used, referenced, or paraphrased.
