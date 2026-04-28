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

Verify — expect a row with `STATUS=Active`:

```bash
kubectl get ns practice
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

Verify — rollout prints `successfully rolled out`; `get deploy` shows `READY 1/1` and `IMAGES=nginx:1.27`:

```bash
kubectl rollout status deployment/web
kubectl get deploy web -o wide
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

Verify — expect `READY 3/3` (may take a few seconds to reach that state):

```bash
kubectl get deploy web
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

Verify — the command prints exactly `nginx:1.27-alpine`:

```bash
kubectl get deploy web -o jsonpath='{.spec.template.spec.containers[0].image}{"\n"}'
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

Verify — `svc` shows `TYPE=ClusterIP` and `PORT(S)=80/TCP`; `endpoints` lists one IP per ready pod (empty `<none>` means the selector matches no ready pods):

```bash
kubectl get svc web
kubectl get endpoints web
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

Verify — expect `STATUS=Running` and `READY 1/1`:

```bash
kubectl get pod tools
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

Verify — rollout reports success and `get deploy` shows `READY 4/4` (proves the edit took effect, not the original `2`):

```bash
kubectl rollout status deployment/api
kubectl get deploy api
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

Verify — output is a JSON map with both keys, e.g. `map[APP_ENV:prod APP_TIER:web]`:

```bash
kubectl get cm app-cfg -o jsonpath='{.data}{"\n"}'
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

Verify — the stored value is base64 (`.data.API_KEY` would print `c3VwZXJzZWNyZXQ=`); decoding it prints `supersecret`:

```bash
kubectl get secret app-sec -o jsonpath='{.data.API_KEY}' | base64 -d; echo
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

Verify — expect `SCHEDULE=*/5 * * * *`, `SUSPEND=False`, and `LAST SCHEDULE` fills in after the first firing (up to 5 min):

```bash
kubectl get cronjob hello-cron
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

### Drill 22 — Init container waits for a service (requires `web` service from Drill 5)
**Budget:** 6 min
**Task:** Assuming the `web` service from Drill 5 already exists, create pod `delayed` so its initContainer blocks until service `web` resolves via DNS, then the main container runs `nginx:1.27`.

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

> **Local env:** Minikube needs a CNI that enforces NetworkPolicy — start with `minikube start -p ckad --cni=calico` if you actually want to test enforcement.
>
> **Real exam:** you do **not** need to enable or pick a CNI — the exam cluster already runs one that enforces NetworkPolicy (Calico/Cilium-class). What matters on exam day:
> 1. **Read the namespace** — apply the policy with `-n <ns>` and confirm it's in the right one (`kubectl get netpol -n <ns>`).
> 2. **Set `policyTypes` explicitly** (`Ingress`, `Egress`, or both). Omitting it means "infer from rules", which is a common silent-fail trap when the question says "deny all egress".
> 3. **Default-deny is empty rules, not missing rules.** To deny all ingress for a selector use `policyTypes: [Ingress]` with **no** `ingress:` key (or `ingress: []`). Same for egress.
> 4. **Selector scoping matters.** `from.podSelector` matches pods in the **same namespace**; cross-namespace requires `namespaceSelector` (often combined with `podSelector`).
> 5. **DNS often needs an explicit egress rule** — UDP/TCP 53 to `kube-system` — when the task asks for restricted egress and the pod uses Service names.
> 6. **Validate by traffic, not by `describe`.** The grader checks behavior:
>    ```bash
>    kubectl run probe --rm -it --image=busybox --restart=Never --labels=role=client \
>      -- wget -qO- --timeout=2 http://web.<ns>.svc.cluster.local || echo BLOCKED
>    ```
> 7. **Don't restart pods.** Policies apply immediately to existing pods — recreating workloads wastes time.
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

> **Local env:** Minikube ships with a default `standard` StorageClass (hostpath), so the PVC binds automatically without creating a PV.
>
> **Real exam:** behavior depends on the task. Two patterns are common — **read the question** to know which:
> 1. **"Create a PVC that binds."** A default StorageClass exists. Don't set `storageClassName` (leave it unset) and the PVC will bind. Confirm:
>    ```bash
>    kubectl get sc                                       # find the (default) class
>    kubectl get pvc <name> -o wide                       # STATUS should be Bound
>    ```
> 2. **"Create a PV and a matching PVC."** No default StorageClass, OR the task asks you to back the claim with a specific PV (often `hostPath`). To force the binding to your PV, both sides must agree:
>    - Use **`storageClassName: ""`** (empty string) on the PVC and PV to opt out of dynamic provisioning, **or** set the same explicit class name on both.
>    - Match `accessModes` and ensure `PV.capacity.storage >= PVC.requests.storage`.
>    - Use a `selector` on the PVC if the task names labels for the PV.
> 3. **`ReadWriteOnce` vs `ReadWriteMany`** — only request RWX if the task says so; most exam clusters' default class only provides RWO and the PVC will hang in `Pending`.
> 4. **`Pending` PVC = read the events.** `kubectl describe pvc <name>` shows the exact mismatch (no class, no matching PV, wrong access mode, capacity too small).
> 5. **Reclaim policy** — only set `persistentVolumeReclaimPolicy: Retain` on the PV when the task explicitly requires data to survive PVC deletion.
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

These 25 drills cover ~80% of the CKAD curriculum. The remaining gaps are covered in [`drills-part2.md`](drills-part2.md):

- **SecurityContext** — `runAsUser`, `runAsNonRoot`, `fsGroup`, `readOnlyRootFilesystem` (drills 26–28).
- **ServiceAccount** — create + bind via `serviceAccountName`, plus RBAC with `auth can-i` (drills 29–30).
- **Ingress** — `networking.k8s.io/v1` path- and host-based rules (drills 31–32).
- **Observability** — `kubectl top` and events sorted by time (drills 33–34).
- **Multi-container patterns** — ambassador and adapter (drills 35–36).
- **`kubectl edit` vs apply-from-file** (drill 37).
