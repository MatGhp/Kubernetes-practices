# CKAD Mock Lab — Modern Drills (Curriculum Gap Fillers)

Follow-up to [`drills-1-core.md`](drills-1-core.md), [`drills-2-advanced.md`](drills-2-advanced.md), and [`drills-3-imperative.md`](drills-3-imperative.md). These 13 drills target topics on the **current** CKAD curriculum that none of the other three files exercise:

- **Deployment strategies** — blue-green, canary
- **Packaging** — Helm install/upgrade/rollback, Kustomize overlays
- **Scheduling** — `nodeSelector`, `nodeAffinity`, `podAntiAffinity`
- **Resource governance** — `LimitRange`
- **Storage** — `StorageClass` + dynamic PVC
- **Custom Resources** — author a CRD and create an instance
- **Resilience** — `PodDisruptionBudget`
- **Modern debug** — `kubectl debug` ephemeral container
- **Autoscaling** — `HorizontalPodAutoscaler` (`kubectl autoscale`)

All scenarios are written from scratch against Kubernetes **1.34+** APIs (current supported branches: 1.34, 1.35, 1.36). Same drill format as part 2: set a timer, solve without peeking, then expand the answer. Every answer ends with a **Verify** block plus a per-drill **Cleanup** so you can reset the namespace between attempts.

> **Why a separate file?** External community drill sources we would normally adapt (e.g. some O'Reilly-affiliated repos) ship without a software license. We can't redistribute or closely paraphrase that content. So this file is **original** material covering the same curriculum domains those sources address — and only the topics still in the current exam blueprint. No standalone `ReplicaSet` drill, no legacy SA-token-Secret drill, no deprecated probe fields.

Assumed setup (same as part 1/2 — see [`README.md`](README.md#21-kubectl-aliases-and-autocompletion) §2.1):

```bash
kubectl create namespace practice 2>/dev/null
kubectl config set-context --current --namespace=practice
alias k=kubectl
export do="--dry-run=client -o yaml"
```

Some drills need cluster add-ons or extra tooling. Enable them once up-front:

```bash
# StorageClass drill — minikube ships a default 'standard' StorageClass via the
# storage-provisioner addon, which is enabled by default. Confirm:
kubectl get storageclass

# Helm drill — install the CLI on the host (Windows: choco install kubernetes-helm)
helm version

# kubectl debug drill — ephemeral containers GA since 1.25 (on by default)
kubectl debug --help >/dev/null && echo "ok"

# HPA drill — metrics-server is required for HPA to compute CPU utilisation
minikube -p ckad addons enable metrics-server
```

---

## Section A — Deployment Strategies

### Drill 38 — Blue-green: flip a Service selector
**Curriculum:** Application Deployment
**Budget:** 4 min
**Task:** Two Deployments `web-blue` and `web-green` (3 replicas each, image `nginx:1.27` and `nginx:1.28`, both label `app=web`, plus distinguishing `version=blue|green`). One Service `web` of type `ClusterIP` initially routes to `version=blue`. Cut traffic to green by editing **only** the Service selector. Reference: [`demos/04-pod-design/04-blue-green-deployment.yaml`](../demos/04-pod-design/04-blue-green-deployment.yaml).

<details><summary>Answer</summary>

```yaml
# web-bg.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: web-blue
spec:
  replicas: 3
  selector:
    matchLabels: { app: web, version: blue }
  template:
    metadata:
      labels: { app: web, version: blue }
    spec:
      containers:
        - name: nginx
          image: nginx:1.27
          ports: [{ containerPort: 80 }]
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: web-green
spec:
  replicas: 3
  selector:
    matchLabels: { app: web, version: green }
  template:
    metadata:
      labels: { app: web, version: green }
    spec:
      containers:
        - name: nginx
          image: nginx:1.28
          ports: [{ containerPort: 80 }]
---
apiVersion: v1
kind: Service
metadata:
  name: web
spec:
  selector: { app: web, version: blue }   # flip to green to cut over
  ports: [{ port: 80, targetPort: 80 }]
```

```bash
kubectl apply -f web-bg.yaml

# Cut over — single-field patch, no restart, no client downtime
kubectl patch svc web -p '{"spec":{"selector":{"app":"web","version":"green"}}}'
```

Verify — endpoints should now point at green pods, and the served version banner from `nginx:1.28` should match:

```bash
kubectl get endpoints web -o wide
kubectl run curl --rm -it --image=curlimages/curl --restart=Never -- \
  -sI http://web | grep -i server
```
</details>

**Cleanup:** `kubectl delete -f web-bg.yaml`

---

### Drill 39 — Canary: 9 stable / 1 canary behind one Service
**Curriculum:** Application Deployment
**Budget:** 4 min
**Task:** One Service `shop` selecting `app=shop` (no version label). Two Deployments `shop-stable` (9 replicas, `nginx:1.27`) and `shop-canary` (1 replica, `nginx:1.28`), both labeled `app=shop` plus `track=stable|canary`. ~10% of traffic should hit canary. Reference: [`demos/04-pod-design/05-canary-deployment.yaml`](../demos/04-pod-design/05-canary-deployment.yaml).

<details><summary>Answer</summary>

```yaml
# shop-canary.yaml
apiVersion: apps/v1
kind: Deployment
metadata: { name: shop-stable }
spec:
  replicas: 9
  selector: { matchLabels: { app: shop, track: stable } }
  template:
    metadata: { labels: { app: shop, track: stable } }
    spec:
      containers:
        - { name: nginx, image: "nginx:1.27", ports: [{ containerPort: 80 }] }
---
apiVersion: apps/v1
kind: Deployment
metadata: { name: shop-canary }
spec:
  replicas: 1
  selector: { matchLabels: { app: shop, track: canary } }
  template:
    metadata: { labels: { app: shop, track: canary } }
    spec:
      containers:
        - { name: nginx, image: "nginx:1.28", ports: [{ containerPort: 80 }] }
---
apiVersion: v1
kind: Service
metadata: { name: shop }
spec:
  selector: { app: shop }   # NB: no `track` — both Deployments are picked up
  ports: [{ port: 80, targetPort: 80 }]
```

```bash
kubectl apply -f shop-canary.yaml
```

Verify — endpoint count should be 10, and ~1/10 hits should land on canary:

```bash
kubectl get endpoints shop -o jsonpath='{.subsets[*].addresses[*].ip}{"\n"}' | wc -w
for i in $(seq 1 30); do
  kubectl exec deploy/shop-stable -- curl -s -o /dev/null -w '%{http_code} %{remote_ip}\n' http://shop
done | sort | uniq -c
```
</details>

**Cleanup:** `kubectl delete -f shop-canary.yaml`

---

## Section B — Packaging

### Drill 40 — Helm: install, upgrade, rollback
**Curriculum:** Application Deployment
**Budget:** 4 min
**Task:** Add the Bitnami repo, install `nginx` as release `web1` with `replicaCount=2`, upgrade to `replicaCount=4`, then roll back to revision 1. List the release history.

<details><summary>Answer</summary>

```bash
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo update

helm install web1 bitnami/nginx --set replicaCount=2
helm upgrade  web1 bitnami/nginx --set replicaCount=4
helm history  web1
helm rollback web1 1
```

Verify — after rollback the Deployment is back to 2 replicas and `helm history` shows a new revision pointing at revision 1:

```bash
kubectl get deploy -l app.kubernetes.io/instance=web1
helm history web1 | tail -3
```
</details>

**Cleanup:** `helm uninstall web1`

---

### Drill 41 — Kustomize overlay
**Curriculum:** Application Deployment
**Budget:** 3 min
**Task:** Create a base with one Deployment (`api`, image `nginx:1.27`, 1 replica) and an overlay `overlays/dev` that bumps replicas to 3 and injects `commonLabels: {env: dev}`. Apply the overlay with `kubectl -k`. Reference: existing examples in [`demos/09-kustomize/`](../demos/09-kustomize/).

<details><summary>Answer</summary>

```text
kustomize-drill/
├── base/
│   ├── deployment.yaml
│   └── kustomization.yaml
└── overlays/dev/
    ├── kustomization.yaml
    └── replicas-patch.yaml
```

```yaml
# base/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata: { name: api }
spec:
  replicas: 1
  selector: { matchLabels: { app: api } }
  template:
    metadata: { labels: { app: api } }
    spec:
      containers:
        - { name: nginx, image: "nginx:1.27" }
```

```yaml
# base/kustomization.yaml
resources: [deployment.yaml]
```

```yaml
# overlays/dev/kustomization.yaml
resources:
  - ../../base
commonLabels:
  env: dev
patches:
  - path: replicas-patch.yaml
```

```yaml
# overlays/dev/replicas-patch.yaml
apiVersion: apps/v1
kind: Deployment
metadata: { name: api }
spec:
  replicas: 3
```

```bash
kubectl apply -k kustomize-drill/overlays/dev
```

Verify — 3 replicas plus the `env=dev` label propagated to the Deployment, ReplicaSet, and Pods:

```bash
kubectl get deploy api -o jsonpath='{.spec.replicas}{"\n"}'
kubectl get pod -l app=api,env=dev
```
</details>

**Cleanup:** `kubectl delete -k kustomize-drill/overlays/dev`

---

## Section C — Scheduling

### Drill 42 — nodeSelector
**Curriculum:** Environment, Configuration & Security
**Budget:** 2 min
**Task:** Label one node with `disktype=ssd`, then schedule a Pod `fast` (image `nginx`) that runs **only** on that node.

<details><summary>Answer</summary>

```bash
NODE=$(kubectl get nodes -o jsonpath='{.items[0].metadata.name}')
kubectl label node "$NODE" disktype=ssd --overwrite
```

```yaml
# fast.yaml
apiVersion: v1
kind: Pod
metadata: { name: fast }
spec:
  nodeSelector: { disktype: ssd }
  containers:
    - { name: nginx, image: nginx }
```

```bash
kubectl apply -f fast.yaml
```

Verify:

```bash
kubectl get pod fast -o wide
kubectl get node -l disktype=ssd
```
</details>

**Cleanup:** `kubectl delete pod fast; kubectl label node "$NODE" disktype-`

---

### Drill 43 — nodeAffinity (required + preferred)
**Curriculum:** Environment, Configuration & Security
**Budget:** 3 min
**Task:** Pod `picky` (image `nginx`) that **must** schedule on a node where `kubernetes.io/os=linux`, and **prefers** a node labeled `zone=eu-west`. Demonstrate that the Pod still schedules even if no node has the preferred label.

<details><summary>Answer</summary>

```yaml
# picky.yaml
apiVersion: v1
kind: Pod
metadata: { name: picky }
spec:
  affinity:
    nodeAffinity:
      requiredDuringSchedulingIgnoredDuringExecution:
        nodeSelectorTerms:
          - matchExpressions:
              - { key: kubernetes.io/os, operator: In, values: [linux] }
      preferredDuringSchedulingIgnoredDuringExecution:
        - weight: 50
          preference:
            matchExpressions:
              - { key: zone, operator: In, values: [eu-west] }
  containers:
    - { name: nginx, image: nginx }
```

Verify — `picky` is `Running`; if you label a node `zone=eu-west` *before* applying, it lands there:

```bash
kubectl apply -f picky.yaml
kubectl get pod picky -o wide
```
</details>

**Cleanup:** `kubectl delete pod picky`

---

### Drill 44 — podAntiAffinity for HA spread
**Curriculum:** Environment, Configuration & Security
**Budget:** 3 min
**Task:** Deployment `ha` (image `nginx`, 3 replicas) whose pods **must not** co-locate on the same node — spread by `topologyKey: kubernetes.io/hostname`. On a single-node minikube, demonstrate that only 1 pod becomes `Running` (the rest stay `Pending`) — that's the *evidence* the rule is enforced.

<details><summary>Answer</summary>

```yaml
# ha.yaml
apiVersion: apps/v1
kind: Deployment
metadata: { name: ha }
spec:
  replicas: 3
  selector: { matchLabels: { app: ha } }
  template:
    metadata: { labels: { app: ha } }
    spec:
      affinity:
        podAntiAffinity:
          requiredDuringSchedulingIgnoredDuringExecution:
            - labelSelector:
                matchLabels: { app: ha }
              topologyKey: kubernetes.io/hostname
      containers:
        - { name: nginx, image: nginx }
```

Verify:

```bash
kubectl apply -f ha.yaml
kubectl get pod -l app=ha -o wide
kubectl describe pod -l app=ha | grep -A2 "didn't match pod anti-affinity"
```
</details>

**Cleanup:** `kubectl delete -f ha.yaml`

---

## Section D — Resource Governance

### Drill 45 — LimitRange populates implicit Pod resources
**Curriculum:** Environment, Configuration & Security
**Budget:** 3 min
**Task:** Create a `LimitRange` named `sane-defaults` in `practice` with `defaultRequest: { cpu: 100m, memory: 64Mi }`, `default: { cpu: 500m, memory: 256Mi }`, `min`/`max` bounds. Then create a Pod **without** explicit `resources` and confirm it inherits the defaults.

<details><summary>Answer</summary>

```yaml
# sane-defaults.yaml
apiVersion: v1
kind: LimitRange
metadata: { name: sane-defaults }
spec:
  limits:
    - type: Container
      defaultRequest: { cpu: 100m, memory: 64Mi }
      default:        { cpu: 500m, memory: 256Mi }
      min:            { cpu: 50m,  memory: 32Mi }
      max:            { cpu: "2",  memory: 1Gi }
```

```bash
kubectl apply -f sane-defaults.yaml
kubectl run no-res --image=nginx --restart=Never
```

Verify — the Pod's container has `requests` and `limits` populated even though the manifest didn't set them:

```bash
kubectl get pod no-res -o jsonpath='{.spec.containers[0].resources}{"\n"}'
```
</details>

**Cleanup:** `kubectl delete pod no-res; kubectl delete limitrange sane-defaults`

---

## Section E — Storage

### Drill 46 — StorageClass + dynamic PVC with WaitForFirstConsumer
**Curriculum:** Environment, Configuration & Security
**Budget:** 3 min
**Task:** Using the cluster's default `StorageClass`, create a PVC `data` (1 Gi, RWO). Note that with `volumeBindingMode: WaitForFirstConsumer` (the minikube default behaviour for the `standard` class) the PVC stays `Pending` until a Pod actually mounts it. Create a Pod that mounts it and observe binding.

<details><summary>Answer</summary>

```bash
# Confirm a default StorageClass exists
kubectl get storageclass
SC=$(kubectl get storageclass -o jsonpath='{.items[?(@.metadata.annotations.storageclass\.kubernetes\.io/is-default-class=="true")].metadata.name}')
echo "default SC: $SC"
```

```yaml
# data.yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata: { name: data }
spec:
  accessModes: [ReadWriteOnce]
  resources: { requests: { storage: 1Gi } }
  # Omitting storageClassName uses the default class
---
apiVersion: v1
kind: Pod
metadata: { name: writer }
spec:
  containers:
    - name: app
      image: busybox
      command: ["sh","-c","echo hello > /data/hello && sleep infinity"]
      volumeMounts: [{ name: vol, mountPath: /data }]
  volumes:
    - name: vol
      persistentVolumeClaim: { claimName: data }
```

```bash
kubectl apply -f data.yaml
```

Verify — PVC transitions `Pending` → `Bound` only once `writer` is scheduled:

```bash
kubectl get pvc data -w   # Ctrl-C once Bound
kubectl exec writer -- cat /data/hello
```
</details>

**Cleanup:** `kubectl delete -f data.yaml`

---

## Section F — Custom Resources

### Drill 47 — Author a CRD and create an instance
**Curriculum:** Environment, Configuration & Security
**Budget:** 4 min
**Task:** Define a `CustomResourceDefinition` for `widgets.example.com` (cluster-scoped is fine, but use Namespaced for this drill) with a small OpenAPI v3 schema (`spec.size` ∈ `small|medium|large`, `spec.color` string). Create a `Widget` CR named `gizmo` and list it. Reference: existing examples in [`demos/08-custom-resource-definition/`](../demos/08-custom-resource-definition/).

<details><summary>Answer</summary>

```yaml
# widget-crd.yaml
apiVersion: apiextensions.k8s.io/v1
kind: CustomResourceDefinition
metadata:
  name: widgets.example.com
spec:
  group: example.com
  scope: Namespaced
  names:
    plural: widgets
    singular: widget
    kind: Widget
    shortNames: [wg]
  versions:
    - name: v1
      served: true
      storage: true
      schema:
        openAPIV3Schema:
          type: object
          properties:
            spec:
              type: object
              required: [size]
              properties:
                size:  { type: string, enum: [small, medium, large] }
                color: { type: string }
```

```yaml
# gizmo.yaml
apiVersion: example.com/v1
kind: Widget
metadata: { name: gizmo }
spec:
  size: medium
  color: blue
```

```bash
kubectl apply -f widget-crd.yaml
kubectl wait --for=condition=Established crd/widgets.example.com --timeout=30s
kubectl apply -f gizmo.yaml
```

Verify — the CR is listed via the new short-name, and an invalid value is rejected by the schema:

```bash
kubectl get wg
kubectl explain widget.spec
# Negative test — should fail with validation error
kubectl apply -f - <<'EOF' || echo "rejected as expected"
apiVersion: example.com/v1
kind: Widget
metadata: { name: bad }
spec: { size: huge }
EOF
```
</details>

**Cleanup:** `kubectl delete -f gizmo.yaml; kubectl delete -f widget-crd.yaml`

---

## Section G — Resilience & Modern Debug

### Drill 48 — PodDisruptionBudget
**Curriculum:** Application Deployment
**Budget:** 2 min
**Task:** Deployment `quorum` (image `nginx`, 3 replicas, label `app=quorum`). Add a `PodDisruptionBudget` `quorum-pdb` requiring `minAvailable: 2`. Confirm `kubectl drain` would respect it (use `--dry-run=server`).

<details><summary>Answer</summary>

```yaml
# quorum.yaml
apiVersion: apps/v1
kind: Deployment
metadata: { name: quorum }
spec:
  replicas: 3
  selector: { matchLabels: { app: quorum } }
  template:
    metadata: { labels: { app: quorum } }
    spec:
      containers: [{ name: nginx, image: nginx }]
---
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata: { name: quorum-pdb }
spec:
  minAvailable: 2
  selector: { matchLabels: { app: quorum } }
```

```bash
kubectl apply -f quorum.yaml
```

Verify — `ALLOWED DISRUPTIONS` should be `1` (3 replicas, min 2 available):

```bash
kubectl get pdb quorum-pdb
```
</details>

**Cleanup:** `kubectl delete -f quorum.yaml`

---

### Drill 49 — `kubectl debug` ephemeral container
**Curriculum:** Application Observability & Maintenance
**Budget:** 3 min
**Task:** A running Pod `target` (image `nginx`, no shell tools beyond what nginx ships). Without restarting it, attach an ephemeral `busybox` container so you can `wget` localhost from inside the Pod's network namespace. Then demonstrate the `--copy-to` variant that creates a debug clone with extra tools and a shell command override.

<details><summary>Answer</summary>

```bash
kubectl run target --image=nginx
kubectl wait --for=condition=Ready pod/target --timeout=30s
```

Ephemeral container — joins the running pod's net + pid namespaces:

```bash
kubectl debug -it target --image=busybox --target=target -- sh
# inside the ephemeral container:
#   wget -qO- http://localhost
#   exit
```

`--copy-to` variant — non-destructive: makes a sibling pod with an added debug image and overridden command, leaving the original untouched. Useful when the target's `command` itself is broken (CrashLoopBackOff scenarios):

```bash
kubectl debug target --copy-to=target-debug --image=busybox \
  --share-processes -- sh -c "sleep 3600"
kubectl exec -it target-debug -c debugger -- sh
```

Verify:

```bash
kubectl get pod target -o jsonpath='{.spec.ephemeralContainers[*].name}{"\n"}'
kubectl get pod target-debug
```
</details>

**Cleanup:** `kubectl delete pod target target-debug`

---

## Section H — Autoscaling

### Drill 49b — Autoscale a Deployment with an HPA
**Curriculum:** Application Deployment
**Budget:** 2 min
**Task:** A Deployment `api` already exists (image `nginx:1.27`, 2 replicas, with CPU `requests: 100m` set on the container — the HPA needs this). Create a HorizontalPodAutoscaler that keeps the Pod count between **2** and **5** and targets **70% CPU utilisation**. Reference: [`demos/04-pod-design/08-hpa-definition.yaml`](../demos/04-pod-design/08-hpa-definition.yaml).

<details><summary>Answer</summary>

Fastest path — imperative `kubectl autoscale`:

```bash
# Setup (only if `api` doesn't already exist)
kubectl create deployment api --image=nginx:1.27 --replicas=2
kubectl set resources deployment api --requests=cpu=100m,memory=64Mi

# The actual answer
kubectl autoscale deployment api --min=2 --max=5 --cpu=70%
```

> `--cpu=70%` replaces the deprecated `--cpu-percent=70` flag (deprecation message added in kubectl 1.34). Use `--cpu=500m` instead if the task asks for an absolute milliCPU target rather than a utilisation percentage.

Equivalent declarative form (`autoscaling/v2`) — use this when the task asks you to write YAML, or when you need non-CPU metrics:

```yaml
# api-hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata: { name: api }
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: api
  minReplicas: 2
  maxReplicas: 5
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
```

```bash
kubectl apply -f api-hpa.yaml
```

Verify — `TARGETS` shows `<current>%/70%`, `MINPODS=2`, `MAXPODS=5`. The `<unknown>` you may see for the first ~30s is normal: metrics-server needs a scrape interval before the first values land.

```bash
kubectl get hpa api
kubectl describe hpa api | sed -n '/Conditions/,/Events/p'
kubectl top pod -l app=api
```

> **Gotchas:**
> - HPA computes utilisation as `usage / requests`. **No CPU `requests` on the Pod → HPA stays `<unknown>` forever.** That's the #1 exam pitfall.
> - HPA needs metrics-server. On minikube: `minikube -p ckad addons enable metrics-server`. On the real exam it is already running — see [README §9.4](README.md#94-metrics-server--kubectl-top-drill-33).
> - `kubectl autoscale` always emits `autoscaling/v1`. The declarative form above uses `autoscaling/v2` so you can add memory or custom metrics later.

</details>

**Cleanup:** `kubectl delete hpa api; kubectl delete deploy api`

---

## Cleanup (full reset)

```bash
kubectl delete deploy,svc,pod,pvc,limitrange,pdb,hpa --all -n practice
kubectl delete crd widgets.example.com 2>/dev/null
helm uninstall web1 2>/dev/null
# Remove the SSD label if you set it in Drill 42
kubectl label node --all disktype- 2>/dev/null
```

---

## Why these drills

Each drill maps to an explicit current-curriculum bullet that wasn't already exercised in [drills-1-core.md](drills-1-core.md), [drills-2-advanced.md](drills-2-advanced.md), or [drills-3-imperative.md](drills-3-imperative.md):

| Drill | Domain | Curriculum bullet |
|---|---|---|
| 38, 39 | Application Deployment 20% | "Use Kubernetes primitives to implement common deployment strategies (e.g. blue/green or canary)" |
| 40, 41 | Application Deployment 20% | "Use Helm and Kustomize to install an existing package" |
| 42–44 | Env/Config/Security 25% | Pod scheduling — node labels, affinity/anti-affinity |
| 45 | Env/Config/Security 25% | Resource requirements, limits, and quotas |
| 46 | Env/Config/Security 25% | Persistent and ephemeral volumes |
| 47 | Env/Config/Security 25% | "Discover and use resources that extend Kubernetes (CRD, Operators)" |
| 48 | Application Deployment 20% | Application robustness — PDB |
| 49 | Observability 15% | "Utilize container logs / debug in Kubernetes" |
| 49b | Application Deployment 20% | "Use the Horizontal Pod Autoscaler to dynamically scale a Deployment" |

Skipped on purpose: standalone `ReplicaSet` (use Deployment), legacy SA-token Secrets (already covered correctly in [demos/01-configuration/12-2-service-account-secret-definition.yaml](../demos/01-configuration/12-2-service-account-secret-definition.yaml)), `PodSecurityPolicy` (removed in 1.25), deprecated probe fields.

---

## Scoring

Same as the other drill files: 2 pts solved in budget without peeking, 1 pt solved within 1.5× or after one peek, 0 pts otherwise. Target **20+ / 26** across this set.
