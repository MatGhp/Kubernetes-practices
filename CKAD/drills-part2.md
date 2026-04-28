# CKAD Mock Lab — Part 2 (Gap Fillers)

Follow-up to [`drills.md`](drills.md). These 12 drills cover the topics intentionally left out of part 1:

- **SecurityContext** — `runAsUser`, `runAsNonRoot`, `fsGroup`, `readOnlyRootFilesystem`
- **ServiceAccount** — create + bind via `serviceAccountName`
- **Ingress** — `networking.k8s.io/v1` with path and host rules
- **Observability** — `kubectl top`, events sorted by time
- **Multi-container patterns** — adapter and ambassador
- **`kubectl edit` vs apply-from-file**

Same rules as part 1: set a timer, solve without peeking, then expand the answer. Every answer ends with a **Verify** block showing exactly what you should see when the change landed.

> Inspired by and cross-checked against the community [`dgkanatsios/CKAD-exercises`](https://github.com/dgkanatsios/CKAD-exercises) reference lab.

Assumed setup (same as part 1 — see [`README.md`](README.md#21-kubectl-aliases-and-autocompletion) §2.1):

```bash
kubectl create namespace practice 2>/dev/null
kubectl config set-context --current --namespace=practice
alias k=kubectl
export do="--dry-run=client -o yaml"
```

Some drills need cluster add-ons. Enable them once up-front:

```bash
# Metrics Server (for `kubectl top`) — takes ~30s to start collecting
minikube -p ckad addons enable metrics-server

# Ingress controller (nginx) for the Ingress drills
minikube -p ckad addons enable ingress
kubectl wait -n ingress-nginx --for=condition=Ready pod \
  -l app.kubernetes.io/component=controller --timeout=120s
```

---

## Section F — SecurityContext

### Drill 26 — Run as non-root with a fixed UID
**Budget:** 5 min
**Task:** Pod `sec-uid` (image `busybox`, sleeps forever) that runs as UID `1000` and **must** fail to start if the image tries to run as root.

<details><summary>Answer</summary>

```yaml
# sec-uid.yaml
apiVersion: v1
kind: Pod
metadata:
  name: sec-uid
spec:
  restartPolicy: Never
  securityContext:
    runAsUser: 1000
    runAsNonRoot: true
  containers:
    - name: app
      image: busybox
      command: ["sh", "-c", "id && sleep infinity"]
```

```bash
kubectl apply -f sec-uid.yaml
```

Verify — logs should print `uid=1000 gid=0(root) ...`; if the image insisted on UID 0 the pod would be in `CreateContainerConfigError` with reason `container has runAsNonRoot and image will run as root`:

```bash
kubectl logs sec-uid
kubectl get pod sec-uid -o jsonpath='{.spec.securityContext}{"\n"}'
```
</details>

---

### Drill 27 — Read-only root filesystem with a writable volume
**Budget:** 6 min
**Task:** Pod `sec-ro` (image `busybox`):
- `readOnlyRootFilesystem: true` on the container
- Mount an `emptyDir` at `/tmp` so the app can still write there
- Command: write `"ok"` to `/tmp/out`, then try to write to `/etc/out` (must fail), then sleep

<details><summary>Answer</summary>

```yaml
# sec-ro.yaml
apiVersion: v1
kind: Pod
metadata:
  name: sec-ro
spec:
  restartPolicy: Never
  volumes:
    - name: scratch
      emptyDir: {}
  containers:
    - name: app
      image: busybox
      command:
        - sh
        - -c
        - "echo ok > /tmp/out && (echo nope > /etc/out || echo 'blocked: read-only fs') && sleep infinity"
      securityContext:
        readOnlyRootFilesystem: true
      volumeMounts:
        - name: scratch
          mountPath: /tmp
```

```bash
kubectl apply -f sec-ro.yaml
```

Verify — logs show `blocked: read-only fs`; `/tmp/out` exists and contains `ok`:

```bash
kubectl logs sec-ro
kubectl exec sec-ro -- cat /tmp/out
```
</details>

---

### Drill 28 — fsGroup on a shared volume
**Budget:** 6 min
**Task:** Pod `sec-fsgroup` with `fsGroup: 2000` and `runAsUser: 1000`. Mount an `emptyDir` at `/data`, write a file there, and confirm it is owned by group `2000`.

<details><summary>Answer</summary>

```yaml
# sec-fsgroup.yaml
apiVersion: v1
kind: Pod
metadata:
  name: sec-fsgroup
spec:
  restartPolicy: Never
  securityContext:
    runAsUser: 1000
    fsGroup: 2000
  volumes:
    - name: data
      emptyDir: {}
  containers:
    - name: app
      image: busybox
      command: ["sh", "-c", "touch /data/file && ls -ln /data && sleep infinity"]
      volumeMounts:
        - name: data
          mountPath: /data
```

```bash
kubectl apply -f sec-fsgroup.yaml
```

Verify — `ls -ln` in the logs shows owner `1000` and **group `2000`** on `/data/file` (that is what `fsGroup` guarantees on mounted volumes):

```bash
kubectl logs sec-fsgroup
```
</details>

---

## Section G — ServiceAccount

### Drill 29 — Create a ServiceAccount and use it
**Budget:** 4 min
**Task:**
1. Create ServiceAccount `deployer` in `practice`.
2. Create pod `sa-pod` (image `nginx:1.27`) that runs under `deployer`.

<details><summary>Answer</summary>

```bash
kubectl create serviceaccount deployer
```

```yaml
# sa-pod.yaml
apiVersion: v1
kind: Pod
metadata:
  name: sa-pod
spec:
  serviceAccountName: deployer
  containers:
    - name: web
      image: nginx:1.27
```

```bash
kubectl apply -f sa-pod.yaml
```

Verify — jsonpath prints exactly `deployer`; the projected token exists under `/var/run/secrets/kubernetes.io/serviceaccount/`:

```bash
kubectl get pod sa-pod -o jsonpath='{.spec.serviceAccountName}{"\n"}'
kubectl exec sa-pod -- ls /var/run/secrets/kubernetes.io/serviceaccount
```
</details>

---

### Drill 30 — ServiceAccount with a read-only Role
**Budget:** 8 min
**Task:** Give `deployer` permission to `get` and `list` pods in `practice` (nothing else). From inside `sa-pod`, confirm it can list pods but **cannot** delete them.

<details><summary>Answer</summary>

```yaml
# role-reader.yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  namespace: practice
  name: pod-reader
rules:
  - apiGroups: [""]
    resources: ["pods"]
    verbs: ["get", "list"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  namespace: practice
  name: deployer-can-read-pods
subjects:
  - kind: ServiceAccount
    name: deployer
    namespace: practice
roleRef:
  kind: Role
  name: pod-reader
  apiGroup: rbac.authorization.k8s.io
```

```bash
kubectl apply -f role-reader.yaml
```

Verify with `kubectl auth can-i` impersonating the SA — expect `yes` / `no`:

```bash
kubectl auth can-i list pods   --as=system:serviceaccount:practice:deployer -n practice
kubectl auth can-i delete pods --as=system:serviceaccount:practice:deployer -n practice
```
</details>

---

## Section H — Ingress (`networking.k8s.io/v1`)

> **Local env (these drills):** assumes the Minikube `ingress` addon is enabled (see the setup block). Resolve hosts via `$(minikube -p ckad ip)` or `/etc/hosts`.
>
> **Real exam:** the cluster you are graded on is **not Minikube**. The notes below describe what changes and how to handle drills 31 & 32 under exam conditions.

#### Exam reality — Ingress (applies to drills 31 & 32)

What differs from this local setup, and what to do about it:

1. **An Ingress controller is already running** — don't deploy one, don't enable an "addon". Confirm and move on:
   ```bash
   kubectl get ingressclass
   kubectl get pods -A | grep -iE 'ingress|nginx|traefik'
   ```
2. **Set `ingressClassName` explicitly** when the task names a class (or one is the cluster default). Without it, the controller may silently ignore your Ingress:
   ```yaml
   spec:
     ingressClassName: nginx   # copy the name from `kubectl get ingressclass`
   ```
3. **Scaffold imperatively, then edit.** Saves you from typos in `pathType` and the nested `service.port.number` block:
   ```bash
   kubectl create ingress app -n <ns> \
     --rule="/=web:80" --rule="/api=api:80" \
     --class=nginx --dry-run=client -o yaml > ing.yaml
   # host-based variant:
   kubectl create ingress app --rule="web.local/*=web:80" --dry-run=client -o yaml
   ```
4. **`pathType` is mandatory** — admission will reject the object without it. Use `Prefix` unless told otherwise.
5. **`backend.service.port` must match the Service** — use `port.number` or `port.name`, whichever the existing Service exposes (`kubectl get svc <name> -o yaml`).
6. **No `minikube ip` and no `/etc/hosts` edits.** You usually can't modify the exam machine, and you don't need to. Validate from a throwaway pod or with curl headers:
   ```bash
   # In-cluster smoke test (works regardless of external DNS):
   kubectl run tmp --rm -it --image=curlimages/curl --restart=Never -- \
     curl -s -H "Host: web.local" http://<ingress-controller-svc>.<ns>.svc/
   # Or, if a NodePort/LoadBalancer is exposed and reachable:
   curl -s --resolve web.local:80:<addr> http://web.local/
   ```
7. **`ADDRESS` may stay empty** in `kubectl get ingress` — it is **not** part of grading. The grader inspects the spec.
8. **Watch the namespace.** Ingress and backend Services must live in the **same namespace** — set `-n <ns>` on every command or `kubectl config set-context --current --namespace=<ns>`.
9. **TLS only if asked**, and only with a Secret the task provides — never generate certificates.
10. **Score on spec, then move on.** If `kubectl get ingress <name> -o yaml` shows the right `ingressClassName`, rules, paths, `pathType`, and ports, you're done.

30-second exam fast-path:

```bash
kubectl get ingressclass                              # learn the class name
kubectl create ingress <name> --class=<class> \
  --rule="<host>/<path>=<svc>:<port>" \
  --dry-run=client -o yaml > ing.yaml
vim ing.yaml                                          # tweak pathType / add rules / TLS
kubectl apply -f ing.yaml
kubectl describe ingress <name> | grep -E 'Class|Host|Path|Backend'
```

---

### Drill 31 — Path-based Ingress
**Budget:** 7 min
**Task:** Expose deployment `web` (from part 1) and a new deployment `api` behind one Ingress:
- `GET /`     → service `web` port 80
- `GET /api`  → service `api` port 80 (strip the prefix is not required)

<details><summary>Answer</summary>

```bash
kubectl create deployment api --image=hashicorp/http-echo \
  -- -text="hello from api" -listen=:80
kubectl expose deployment api --port=80 --target-port=80
```

```yaml
# ingress-path.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: app
spec:
  rules:
    - http:
        paths:
          - path: /api
            pathType: Prefix
            backend:
              service:
                name: api
                port:
                  number: 80
          - path: /
            pathType: Prefix
            backend:
              service:
                name: web
                port:
                  number: 80
```

```bash
kubectl apply -f ingress-path.yaml
```

Verify — `get ingress` shows a non-empty `ADDRESS`; curl returns the nginx welcome page for `/` and `hello from api` for `/api`:

```bash
kubectl get ingress app
IP=$(minikube -p ckad ip)
curl -s http://$IP/       | head -n 1
curl -s http://$IP/api
```
</details>

---

### Drill 32 — Host-based Ingress
**Budget:** 6 min
**Task:** Change the Ingress so that `http://web.local` → `web` and `http://api.local` → `api`.

<details><summary>Answer</summary>

```yaml
# ingress-host.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: app
spec:
  rules:
    - host: web.local
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service: { name: web, port: { number: 80 } }
    - host: api.local
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service: { name: api, port: { number: 80 } }
```

```bash
kubectl apply -f ingress-host.yaml
```

Verify — `describe ingress` lists both hosts; curl with `--resolve` reaches the right backend based on the `Host` header:

```bash
kubectl describe ingress app | grep -E 'Host|Path'
IP=$(minikube -p ckad ip)
curl -s --resolve web.local:80:$IP http://web.local/ | head -n 1
curl -s --resolve api.local:80:$IP http://api.local/
```
</details>

---

## Section I — Observability

### Drill 33 — `kubectl top` pods and nodes
**Budget:** 3 min
**Task:** Show CPU and memory usage for every pod in `practice`, sorted by CPU descending. Then show the same for the node.

<details><summary>Answer</summary>

```bash
kubectl top pod --sort-by=cpu
kubectl top node
```

Verify — both commands print a table with `CPU(cores)` and `MEMORY(bytes)` columns. If you see `error: Metrics API not available`, the metrics-server addon is not ready yet — wait 30–60s and retry:

```bash
kubectl -n kube-system get deploy metrics-server
kubectl -n kube-system rollout status deploy/metrics-server
```

> **Real exam:** metrics-server is already installed and running. You don't `enable` anything. If `kubectl top` returns `Metrics API not available`:
> 1. Check the deployment is healthy: `kubectl -n kube-system get deploy metrics-server`.
> 2. Wait ~30s — metrics need a scrape interval before the first values appear, especially if pods just started.
> 3. Don't troubleshoot it further — it's not part of the question. Move on, return at the end if needed.
</details>

---

### Drill 34 — Events sorted by time
**Budget:** 3 min
**Task:** Print the 20 most recent events in the `practice` namespace, newest last, then the most recent events for pod `web`.

<details><summary>Answer</summary>

```bash
kubectl get events --sort-by=.lastTimestamp | tail -n 20
kubectl get events --field-selector involvedObject.name=web --sort-by=.lastTimestamp
```

Verify — the last column (`MESSAGE`) tells the story in chronological order (e.g. `Scheduled` → `Pulling` → `Created` → `Started`). For a broken pod you would see `Failed` / `BackOff`.
</details>

---

## Section J — Multi-container patterns

### Drill 35 — Ambassador pattern
**Budget:** 8 min
**Task:** Pod `ambassador-demo` where the **app** only talks to `localhost:8080`, and an **ambassador** container proxies that to the external service `web:80`. Use `nginx` as the proxy, app is `busybox` that curls `http://127.0.0.1:8080`.

<details><summary>Answer</summary>

```yaml
# ambassador.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: amb-proxy
data:
  default.conf: |
    server {
      listen 8080;
      location / {
        proxy_pass http://web:80;
      }
    }
---
apiVersion: v1
kind: Pod
metadata:
  name: ambassador-demo
spec:
  restartPolicy: Never
  volumes:
    - name: cfg
      configMap:
        name: amb-proxy
  containers:
    - name: app
      image: busybox
      command:
        - sh
        - -c
        - "while true; do wget -qO- http://127.0.0.1:8080 | head -n 1; sleep 5; done"
    - name: ambassador
      image: nginx:1.27
      ports:
        - containerPort: 8080
      volumeMounts:
        - name: cfg
          mountPath: /etc/nginx/conf.d
```

```bash
kubectl apply -f ambassador.yaml
```

Verify — the app container logs the nginx welcome line every 5s; that line only comes from `web` via the local-loopback proxy. Ambassador access logs show inbound requests on port 8080:

```bash
kubectl logs ambassador-demo -c app --tail=3
kubectl logs ambassador-demo -c ambassador --tail=3
```
</details>

---

### Drill 36 — Adapter pattern
**Budget:** 8 min
**Task:** Pod `adapter-demo` where the **app** writes plain lines to `/var/log/app.log`, and an **adapter** container transforms each line into a structured JSON line and writes it to `/var/log/app.json`. Share logs via `emptyDir`.

<details><summary>Answer</summary>

```yaml
# adapter.yaml
apiVersion: v1
kind: Pod
metadata:
  name: adapter-demo
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
        - 'while true; do echo "$(date -Iseconds) hello world" >> /var/log/app.log; sleep 2; done'
      volumeMounts:
        - { name: logs, mountPath: /var/log }
    - name: adapter
      image: busybox
      command:
        - sh
        - -c
        - |
          : > /var/log/app.json
          tail -F /var/log/app.log | while read ts msg; do
            printf '{"ts":"%s","msg":"%s"}\n' "$ts" "$msg" >> /var/log/app.json
          done
      volumeMounts:
        - { name: logs, mountPath: /var/log }
```

```bash
kubectl apply -f adapter.yaml
```

Verify — the raw log holds plain lines; the adapted log holds JSON records with the same content:

```bash
kubectl exec adapter-demo -c adapter -- tail -n 3 /var/log/app.log
kubectl exec adapter-demo -c adapter -- tail -n 3 /var/log/app.json
```
</details>

---

## Section K — `kubectl edit` vs apply-from-file

### Drill 37 — Edit a live object, then reconcile from file
**Budget:** 6 min
**Task:**
1. Scale deployment `web` to 5 replicas **using `kubectl edit`** (not `scale`, not `apply`).
2. Produce a file `web.yaml` that matches the current state.
3. Change replicas back to 2 in the file and `apply` it.
4. Explain, in the verify step, why you usually prefer `apply -f` over `edit`.

<details><summary>Answer</summary>

```bash
# 1) Live edit — opens $EDITOR (vi by default). Change spec.replicas: 5 and save.
kubectl edit deployment web

# 2) Snapshot the live object into a clean file (strip server-managed fields).
kubectl get deployment web -o yaml \
  | kubectl neat 2>/dev/null \
  | tee web.yaml >/dev/null \
  || kubectl get deployment web -o yaml > web.yaml

# 3) Edit replicas -> 2 in web.yaml, then:
kubectl apply -f web.yaml
kubectl rollout status deployment/web
```

Verify — right after step 1 you expect `READY 5/5`, and after step 3 `READY 2/2`. Also, `apply` now owns the fields it wrote (visible under `.metadata.managedFields` with manager `kubectl-client-side-apply` or `kubectl`):

```bash
kubectl get deploy web
kubectl get deploy web -o jsonpath='{.metadata.managedFields[*].manager}{"\n"}'
```

> Why prefer `apply -f` over `edit`:
>
> - **Reviewable.** The YAML lives in git; diffs are visible in PRs.
> - **Reproducible.** You can recreate the object in another cluster with the same file.
> - **Safer rollbacks.** `kubectl apply` uses server-side 3-way merge and respects `managedFields`, so other controllers' changes are not silently overwritten.
> - `kubectl edit` is fine for quick debugging, but treat it as throwaway — always fold the change back into the source file.
</details>

---

## Cleanup

```bash
kubectl delete ingress,deploy,svc,pod,cm,sa,role,rolebinding --all -n practice
```

---

## Scoring

Same as part 1: 2 pts solved in budget without peeking, 1 pt solved within 1.5× or after one peek, 0 pts otherwise. Target **20+ / 24** across this set.
