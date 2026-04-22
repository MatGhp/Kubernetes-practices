# CKAD Mock Lab — 25 Drills

A self-contained drill sheet. Each task has a **time budget** and a hidden **answer** (`<details>` block). Try the task first, then expand to check.

> Tip: run all drills in a reset Minikube profile. See [`README.md`](README.md) §5.3.

Assumed setup before any drill (see [`README.md`](README.md#21-kubectl-aliases-and-autocompletion) §2.1 for the full block):

```bash
# Namespace
kubectl create namespace practice
kubectl config set-context --current --namespace=practice

# Alias + common flags
alias k=kubectl
export do="--dry-run=client -o yaml"
export now="--grace-period=0 --force"

# Autocomplete for kubectl AND k
source <(kubectl completion bash)
complete -F __start_kubectl k
```

All answers assume the current namespace is `practice`.

---

## How to use this lab

1. Pick **5 drills** at random.
2. Set a **timer** per drill (see budgets).
3. Solve without opening the answer. Use only:
   - `kubectl --help`
   - `kubectl explain`
   - Kubernetes docs
4. Only expand the answer after you finish **or** time runs out.
5. Log each miss (`drill`, `mistake`, `faster command`).

---

## Section A — Core workloads

### Drill 1 — Create a namespace
**Budget:** 1 min
**Task:** Create a namespace called `practice`.

<details><summary>Answer</summary>

```bash
kubectl create namespace practice
```
</details>

---

### Drill 2 — Create a deployment
**Budget:** 2 min
**Task:** Create a deployment `web` using image `nginx:1.27` with 1 replica.

<details><summary>Answer</summary>

```bash
kubectl create deployment web --image=nginx:1.27
```
</details>

---

### Drill 3 — Scale a deployment
**Budget:** 1 min
**Task:** Scale `web` to 3 replicas.

<details><summary>Answer</summary>

```bash
kubectl scale deployment web --replicas=3
```
</details>

---

### Drill 4 — Update the image
**Budget:** 2 min
**Task:** Change the container image of `web` to `nginx:1.27-alpine`.

<details><summary>Answer</summary>

```bash
kubectl set image deployment/web nginx=nginx:1.27-alpine
kubectl rollout status deployment/web
```
</details>

---

### Drill 5 — Expose a deployment
**Budget:** 2 min
**Task:** Create a ClusterIP service for `web` exposing port 80 → targetPort 80.

<details><summary>Answer</summary>

```bash
kubectl expose deployment web --port=80 --target-port=80
```
</details>

---

### Drill 6 — Create a pod that sleeps
**Budget:** 2 min
**Task:** Create a pod `tools` from `busybox` that sleeps forever.

<details><summary>Answer</summary>

```bash
kubectl run tools --image=busybox --restart=Never -- /bin/sh -c "sleep infinity"
```
</details>

---

### Drill 7 — Exec into a pod
**Budget:** 2 min
**Task:** Exec into `tools` and reach the `web` service with `wget`.

<details><summary>Answer</summary>

```bash
kubectl exec -it tools -- sh
# inside:
wget -qO- http://web:80
```
</details>

---

### Drill 8 — Print pod labels
**Budget:** 1 min
**Task:** Show labels of every pod belonging to the `web` deployment.

<details><summary>Answer</summary>

```bash
kubectl get pods -l app=web --show-labels
```
</details>

---

### Drill 9 — Rollout undo
**Budget:** 2 min
**Task:** Undo the last rollout of `web`.

<details><summary>Answer</summary>

```bash
kubectl rollout history deployment/web
kubectl rollout undo deployment/web
kubectl rollout status deployment/web
```
</details>

---

### Drill 10 — Generate YAML, edit, apply
**Budget:** 5 min
**Task:** Produce a deployment YAML for `api` (image `nginx`, 2 replicas), change replicas to 4 in `vim`, then apply.

<details><summary>Answer</summary>

```bash
kubectl create deployment api --image=nginx --replicas=2 $do > api.yaml
vim api.yaml   # change spec.replicas from 2 to 4, :wq
kubectl apply -f api.yaml
```
</details>

---

## Section B — Config and secrets

### Drill 11 — ConfigMap from literals
**Budget:** 2 min
**Task:** Create ConfigMap `app-cfg` with `APP_ENV=prod` and `APP_TIER=web`.

<details><summary>Answer</summary>

```bash
kubectl create configmap app-cfg \
  --from-literal=APP_ENV=prod \
  --from-literal=APP_TIER=web
```
</details>

---

### Drill 12 — ConfigMap as env vars
**Budget:** 5 min
**Task:** Create pod `cm-env` (image `busybox`) that sleeps and exposes all `app-cfg` keys as env vars. Verify with `env` inside the pod.

<details><summary>Answer</summary>

```yaml
# cm-env.yaml
apiVersion: v1
kind: Pod
metadata:
  name: cm-env
spec:
  restartPolicy: Never
  containers:
    - name: app
      image: busybox
      command: ["sh", "-c", "sleep infinity"]
      envFrom:
        - configMapRef:
            name: app-cfg
```

```bash
kubectl apply -f cm-env.yaml
kubectl exec cm-env -- env | grep APP_
```
</details>

---

### Drill 13 — ConfigMap as a volume
**Budget:** 5 min
**Task:** Mount `app-cfg` as a read-only volume at `/etc/app-cfg` in pod `cm-vol`.

<details><summary>Answer</summary>

```yaml
# cm-vol.yaml
apiVersion: v1
kind: Pod
metadata:
  name: cm-vol
spec:
  restartPolicy: Never
  containers:
    - name: app
      image: busybox
      command: ["sh", "-c", "sleep infinity"]
      volumeMounts:
        - name: cfg
          mountPath: /etc/app-cfg
          readOnly: true
  volumes:
    - name: cfg
      configMap:
        name: app-cfg
```

```bash
kubectl apply -f cm-vol.yaml
kubectl exec cm-vol -- ls /etc/app-cfg
kubectl exec cm-vol -- cat /etc/app-cfg/APP_ENV
```
</details>

---

### Drill 14 — Secret from literals
**Budget:** 2 min
**Task:** Create Secret `app-sec` with `API_KEY=supersecret`.

<details><summary>Answer</summary>

```bash
kubectl create secret generic app-sec --from-literal=API_KEY=supersecret
```
</details>

---

### Drill 15 — Consume a Secret
**Budget:** 5 min
**Task:** Pod `sec-pod` (image `busybox`) exposes `API_KEY` from the Secret as an env var.

<details><summary>Answer</summary>

```yaml
# sec-pod.yaml
apiVersion: v1
kind: Pod
metadata:
  name: sec-pod
spec:
  restartPolicy: Never
  containers:
    - name: app
      image: busybox
      command: ["sh", "-c", "sleep infinity"]
      env:
        - name: API_KEY
          valueFrom:
            secretKeyRef:
              name: app-sec
              key: API_KEY
```

```bash
kubectl apply -f sec-pod.yaml
kubectl exec sec-pod -- sh -c 'echo $API_KEY'
```
</details>

---

## Section C — Probes, resources, Jobs

### Drill 16 — Readiness & liveness probes
**Budget:** 6 min
**Task:** Pod `probed` (image `nginx:1.27`) with:
- `readinessProbe` httpGet `/` port 80 (delay 2s, period 5s)
- `livenessProbe` httpGet `/` port 80 (delay 10s, period 10s)

<details><summary>Answer</summary>

```yaml
# probed.yaml
apiVersion: v1
kind: Pod
metadata:
  name: probed
spec:
  containers:
    - name: web
      image: nginx:1.27
      ports:
        - containerPort: 80
      readinessProbe:
        httpGet: { path: /, port: 80 }
        initialDelaySeconds: 2
        periodSeconds: 5
      livenessProbe:
        httpGet: { path: /, port: 80 }
        initialDelaySeconds: 10
        periodSeconds: 10
```

```bash
kubectl apply -f probed.yaml
kubectl describe pod probed | grep -E 'Readiness|Liveness'
```
</details>

---

### Drill 17 — Resource requests and limits
**Budget:** 3 min
**Task:** Pod `rsrc` (image `nginx:1.27`) with requests `cpu=100m, memory=128Mi` and limits `cpu=250m, memory=256Mi`.

<details><summary>Answer</summary>

```yaml
# rsrc.yaml
apiVersion: v1
kind: Pod
metadata:
  name: rsrc
spec:
  containers:
    - name: rsrc
      image: nginx:1.27
      resources:
        requests:
          cpu: "100m"
          memory: "128Mi"
        limits:
          cpu: "250m"
          memory: "256Mi"
```

```bash
kubectl apply -f rsrc.yaml
kubectl get pod rsrc -o jsonpath='{.spec.containers[0].resources}'
```
</details>

---

### Drill 18 — One-shot Job
**Budget:** 3 min
**Task:** Create Job `hello` that runs `echo hello` once and completes.

<details><summary>Answer</summary>

```bash
kubectl create job hello --image=busybox -- echo hello
kubectl logs job/hello
```
</details>

---

### Drill 19 — CronJob every 5 minutes
**Budget:** 3 min
**Task:** Create CronJob `hello-cron` that runs `echo hello` every 5 minutes.

<details><summary>Answer</summary>

```bash
kubectl create cronjob hello-cron \
  --image=busybox \
  --schedule="*/5 * * * *" \
  -- echo hello
```
</details>

---

### Drill 20 — Debug a failing pod
**Budget:** 6 min

**Setup (run before starting the timer):**

```bash
kubectl run broken --image=nginx:doesnotexist
kubectl get pod broken   # should show ImagePullBackOff / ErrImagePull
```

**Task:** Pod `broken` is in `ImagePullBackOff`. Diagnose and fix to `nginx:1.27`.

<details><summary>Answer</summary>

```bash
kubectl describe pod broken | tail -n 20   # check Events
kubectl delete pod broken
kubectl run broken --image=nginx:1.27
```

If the broken workload were a Deployment instead, the fix would be:

```bash
kubectl set image deployment/broken nginx=nginx:1.27
kubectl rollout status deployment/broken
```
</details>

---

## Section D — Multi-container and networking

### Drill 21 — Sidecar that tails a shared log
**Budget:** 7 min
**Task:** Pod `logger` with two containers sharing an `emptyDir`:
- `app` (busybox) writes a line every 2s to `/var/log/app.log`
- `sidecar` (busybox) runs `tail -F /var/log/app.log`

<details><summary>Answer</summary>

```yaml
# logger.yaml
apiVersion: v1
kind: Pod
metadata:
  name: logger
spec:
  restartPolicy: Never
  volumes:
    - name: logs
      emptyDir: {}
  containers:
    - name: app
      image: busybox
      command:
        - sh
        - -c
        - "while true; do date >> /var/log/app.log; sleep 2; done"
      volumeMounts:
        - name: logs
          mountPath: /var/log
    - name: sidecar
      image: busybox
      command: ["sh", "-c", "tail -F /var/log/app.log"]
      volumeMounts:
        - name: logs
          mountPath: /var/log
```

```bash
kubectl apply -f logger.yaml
kubectl logs logger -c sidecar --tail=5
```
</details>

---

### Drill 22 — Init container waits for a service
**Budget:** 6 min
**Task:** Pod `delayed` — initContainer blocks until service `web` resolves via DNS, then main container runs `nginx:1.27`.

<details><summary>Answer</summary>

```yaml
# delayed.yaml
apiVersion: v1
kind: Pod
metadata:
  name: delayed
spec:
  initContainers:
    - name: wait-web
      image: busybox
      command:
        - sh
        - -c
        - "until nslookup web; do echo waiting; sleep 2; done"
  containers:
    - name: main
      image: nginx:1.27
```

```bash
kubectl apply -f delayed.yaml
kubectl get pod delayed -w   # stays in Init: until service `web` exists
```

> Requires the `web` service from Drill 5. If it does not exist, the initContainer loops forever — that is the point.
</details>

---

### Drill 23 — NetworkPolicy — allow only from labeled pods
**Budget:** 6 min
**Task:** NetworkPolicy `allow-web-from-client` — allow ingress to pods with `app=web` **only** from pods with `role=client`, on port 80.

<details><summary>Answer</summary>

```yaml
# netpol.yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: allow-web-from-client
spec:
  podSelector:
    matchLabels:
      app: web
  policyTypes: ["Ingress"]
  ingress:
    - from:
        - podSelector:
            matchLabels:
              role: client
      ports:
        - protocol: TCP
          port: 80
```

```bash
kubectl apply -f netpol.yaml
kubectl describe netpol allow-web-from-client
```

> Minikube needs a CNI that enforces NetworkPolicy. Start with `minikube start -p ckad --cni=calico` if enforcement is required.
</details>

---

### Drill 24 — Port-forward to a service
**Budget:** 2 min
**Task:** Port-forward localhost `8080` to `web` service port `80`, then curl it.

<details><summary>Answer</summary>

```bash
kubectl port-forward svc/web 8080:80 &
curl http://localhost:8080
kill %1
```
</details>

---

## Section E — Storage

### Drill 25 — PVC persistence round-trip
**Budget:** 8 min
**Task:**
1. Create PVC `data-pvc` (1Gi, `ReadWriteOnce`).
2. Pod `writer` mounts it at `/data` and writes `"hello"` to `/data/out.txt`.
3. Delete the pod.
4. Pod `reader` with the same PVC confirms the file still exists.

<details><summary>Answer</summary>

```yaml
# pvc.yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: data-pvc
spec:
  accessModes: ["ReadWriteOnce"]
  resources:
    requests:
      storage: 1Gi
```

```yaml
# writer.yaml
apiVersion: v1
kind: Pod
metadata:
  name: writer
spec:
  restartPolicy: Never
  volumes:
    - name: data
      persistentVolumeClaim:
        claimName: data-pvc
  containers:
    - name: app
      image: busybox
      command: ["sh", "-c", "echo hello > /data/out.txt && sleep infinity"]
      volumeMounts:
        - name: data
          mountPath: /data
```

```yaml
# reader.yaml
apiVersion: v1
kind: Pod
metadata:
  name: reader
spec:
  restartPolicy: Never
  volumes:
    - name: data
      persistentVolumeClaim:
        claimName: data-pvc
  containers:
    - name: app
      image: busybox
      command: ["sh", "-c", "cat /data/out.txt && sleep infinity"]
      volumeMounts:
        - name: data
          mountPath: /data
```

```bash
kubectl apply -f pvc.yaml
kubectl apply -f writer.yaml
kubectl wait --for=condition=Ready pod/writer
kubectl delete pod writer
kubectl apply -f reader.yaml
kubectl logs reader   # expect: hello
```

> Minikube ships with a default `standard` StorageClass (hostpath), so the PVC binds without creating a PV manually.
</details>

---

## Scoring

- Solved within budget, no peek → **2 pts**
- Solved within 1.5× budget, or peeked once → **1 pt**
- Did not solve, or used full answer → **0 pts**

Target for exam-readiness: **40+ / 50** across a full 25-drill run.

---

## Cleanup between drill sets

```bash
kubectl delete all,cm,secret,pvc,netpol --all -n practice
# or fully reset the cluster:
minikube delete -p ckad && minikube start -p ckad --driver=docker
```

---

## Coverage — what is and isn't in this lab

These 25 drills cover ~80% of the CKAD curriculum. Intentionally **out of scope** here (practice separately from the main [README.md](README.md) §6 list or the Kubernetes docs):

- **SecurityContext** — `runAsUser`, `runAsNonRoot`, `fsGroup`, `readOnlyRootFilesystem`.
- **ServiceAccount** — creating one and binding it to a pod via `serviceAccountName`.
- **Ingress** — `networking.k8s.io/v1` Ingress resource + path/host rules.
- **Observability** — `kubectl top pod/node` (needs metrics-server), `kubectl get events --sort-by=...`.
- **Multi-container patterns** beyond sidecar — adapter and ambassador.
- **`kubectl edit`** on a live object vs apply-from-file.

Add one drill per gap when you rotate this lab.
