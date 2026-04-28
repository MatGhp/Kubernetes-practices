# CKAD Drills — Community Gap-Fillers (Drills 50–61)

These 12 drills target curriculum corners that the previous five files don't drill hard. Topics and example phrasings were inspired by the open-source [`dgkanatsios/CKAD-exercises`](https://github.com/dgkanatsios/CKAD-exercises) (MIT-licensed) — the **scenarios below are original**, written from scratch for Kubernetes 1.34+ (current supported branches: 1.34/1.35/1.36), but credit goes to that repo for highlighting the gaps.

> **Attribution:** Topic list seeded from `dgkanatsios/CKAD-exercises`, licensed under the [MIT License](https://github.com/dgkanatsios/CKAD-exercises/blob/main/LICENSE). © Dimitris Kanatsios and contributors. We adapted the curriculum coverage map only — no question text or solution code is copied.

Same ground-rules as part 1–4: timer on, hide the answer, run the verify block, log your time. Estimated total: **42 min**.

```bash
kubectl create namespace practice 2>/dev/null
kubectl config set-context --current --namespace=practice
alias k=kubectl
export do="--dry-run=client -o yaml"
```

---

## Section A — Discovery & Documentation

### Drill 50 — `kubectl explain` deep-dive
**Curriculum:** Application Design & Build
**Budget:** 2 min
**Task:** Without opening a browser, find:
1. The full field path for a Pod's container `livenessProbe.httpGet.path`.
2. The valid values for a `Service.spec.type`.
3. The default value of a Job's `spec.completions`.

<details><summary>Answer</summary>

```bash
kubectl explain pod.spec.containers.livenessProbe.httpGet.path
kubectl explain svc.spec.type
kubectl explain job.spec.completions   # default 1
```

`--recursive` is your friend when you forget a field path:

```bash
kubectl explain pod.spec --recursive | grep -A1 livenessProbe | head
```
</details>

**Cleanup:** none (read-only).

---

## Section B — Pods & Multi-container Patterns

### Drill 51 — `command` vs `args` (exec-form)
**Curriculum:** Application Design & Build
**Budget:** 2 min
**Task:** Run a Pod `greeter` (`busybox:1.36`) that prints `hello $NAME`, where `NAME=world` is set via env var. Use `command` to override the entrypoint to `/bin/sh -c` and `args` to pass the script. Pod must terminate cleanly (`Completed`).

<details><summary>Answer</summary>

```yaml
# greeter.yaml
apiVersion: v1
kind: Pod
metadata: { name: greeter }
spec:
  restartPolicy: Never
  containers:
    - name: g
      image: busybox:1.36
      env: [{ name: NAME, value: world }]
      command: ["/bin/sh","-c"]
      args:    ['echo "hello $NAME"']
```

```bash
kubectl apply -f greeter.yaml
kubectl wait --for=condition=Initialized pod/greeter --timeout=10s
kubectl logs greeter
kubectl get pod greeter   # STATUS Completed
```
</details>

**Cleanup:** `kubectl delete pod greeter`

---

### Drill 52 — Downward API as files
**Curriculum:** Environment, Configuration & Security
**Budget:** 3 min
**Task:** Pod `meta` (`busybox:1.36`, sleeps forever) with a `downwardAPI` volume mounted at `/etc/podinfo` exposing the Pod's name and labels (label `app=meta` set in metadata). `cat /etc/podinfo/name` from inside the Pod must print `meta`.

<details><summary>Answer</summary>

```yaml
# meta.yaml
apiVersion: v1
kind: Pod
metadata:
  name: meta
  labels: { app: meta }
spec:
  containers:
    - name: c
      image: busybox:1.36
      command: ["sleep","3600"]
      volumeMounts: [{ name: podinfo, mountPath: /etc/podinfo, readOnly: true }]
  volumes:
    - name: podinfo
      downwardAPI:
        items:
          - path: name
            fieldRef: { fieldPath: metadata.name }
          - path: labels
            fieldRef: { fieldPath: metadata.labels }
```

```bash
kubectl apply -f meta.yaml
kubectl exec meta -- cat /etc/podinfo/name
kubectl exec meta -- cat /etc/podinfo/labels
```
</details>

**Cleanup:** `kubectl delete pod meta`

---

## Section C — Storage Edge Cases

### Drill 53 — `subPath` mount
**Curriculum:** Environment, Configuration & Security
**Budget:** 3 min
**Task:** Mount a single key from a ConfigMap as a file at a path that doesn't overwrite the rest of the directory. ConfigMap `nginxconf` has key `default.conf` (the body of an nginx server block). Mount **only** that key at `/etc/nginx/conf.d/default.conf` in a `nginx:1.27` Pod `nginx-conf` — `/etc/nginx/conf.d/` must still contain its other default files.

<details><summary>Answer</summary>

```bash
kubectl create cm nginxconf \
  --from-literal=default.conf='server { listen 8080; location / { return 200 "ok\n"; } }'
```

```yaml
# nginx-conf.yaml
apiVersion: v1
kind: Pod
metadata: { name: nginx-conf }
spec:
  containers:
    - name: nginx
      image: nginx:1.27
      ports: [{ containerPort: 8080 }]
      volumeMounts:
        - name: cfg
          mountPath: /etc/nginx/conf.d/default.conf
          subPath: default.conf
  volumes:
    - name: cfg
      configMap:
        name: nginxconf
        items:
          - { key: default.conf, path: default.conf }
```

```bash
kubectl apply -f nginx-conf.yaml
kubectl wait --for=condition=Ready pod/nginx-conf --timeout=30s
kubectl exec nginx-conf -- ls /etc/nginx/conf.d/   # default.conf present, others intact
kubectl exec nginx-conf -- curl -s localhost:8080
```
</details>

**Cleanup:** `kubectl delete pod nginx-conf cm/nginxconf`

---

## Section D — Services & DNS

### Drill 54 — Resolve a Service from a Pod
**Curriculum:** Services & Networking
**Budget:** 3 min
**Task:** Service `web` (ClusterIP, port 80) in namespace `practice`. From a one-shot debug Pod in **another** namespace `client-ns`, look up `web` using its FQDN. Demonstrate that the short name fails outside the Service's namespace but the FQDN works.

<details><summary>Answer</summary>

```bash
kubectl create deploy web --image=nginx:1.27 -n practice
kubectl expose deploy web --port=80 -n practice
kubectl create ns client-ns
```

```bash
# Short name from a different namespace — FAILS
kubectl run dns -n client-ns --rm -it --restart=Never --image=busybox:1.36 -- \
  nslookup web

# FQDN works
kubectl run dns -n client-ns --rm -it --restart=Never --image=busybox:1.36 -- \
  nslookup web.practice.svc.cluster.local
```
</details>

**Cleanup:** `kubectl delete ns client-ns; kubectl delete deploy/web svc/web -n practice`

---

### Drill 55 — `ExternalName` Service
**Curriculum:** Services & Networking
**Budget:** 2 min
**Task:** Create a Service `gh` of type `ExternalName` that maps to `api.github.com`. From a debug Pod, `nslookup gh.practice.svc.cluster.local` should return the `api.github.com` CNAME.

<details><summary>Answer</summary>

```yaml
# gh.yaml
apiVersion: v1
kind: Service
metadata: { name: gh }
spec:
  type: ExternalName
  externalName: api.github.com
```

```bash
kubectl apply -f gh.yaml
kubectl run dns --rm -it --restart=Never --image=busybox:1.36 -- \
  nslookup gh.practice.svc.cluster.local
# Expect a CNAME record pointing at api.github.com
```
</details>

**Cleanup:** `kubectl delete svc gh`

---

## Section E — Deployment Mechanics

### Drill 56 — `rollout pause` and `resume`
**Curriculum:** Application Deployment
**Budget:** 3 min
**Task:** Deployment `flow` (`nginx:1.27`, 4 replicas). Pause the rollout, change the image to `nginx:1.28`, observe that **no** new ReplicaSet is created, then resume and watch the rollout complete.

<details><summary>Answer</summary>

```bash
kubectl create deploy flow --image=nginx:1.27 --replicas=4
kubectl rollout pause deploy/flow

kubectl set image deploy/flow nginx=nginx:1.28
kubectl get rs -l app=flow   # still only 1 RS

kubectl rollout resume deploy/flow
kubectl rollout status deploy/flow
kubectl get rs -l app=flow   # now 2 RSes
```
</details>

**Cleanup:** `kubectl delete deploy flow`

---

### Drill 57 — Annotations vs Labels in practice
**Curriculum:** Application Deployment
**Budget:** 3 min
**Task:** Deployment `tagged`: each Pod must carry **label** `app=tagged` and **annotation** `team.example.com/owner=platform`. Demonstrate that a label selector finds the Pod but an annotation cannot be selected against — annotations are metadata only.

<details><summary>Answer</summary>

```yaml
# tagged.yaml
apiVersion: apps/v1
kind: Deployment
metadata: { name: tagged }
spec:
  replicas: 1
  selector: { matchLabels: { app: tagged } }
  template:
    metadata:
      labels: { app: tagged }
      annotations: { team.example.com/owner: platform }
    spec:
      containers: [{ name: nginx, image: nginx:1.27 }]
```

```bash
kubectl apply -f tagged.yaml
kubectl get pod -l app=tagged                              # works
kubectl get pod -l team.example.com/owner=platform 2>&1 | head -1   # 0 results — annotations don't index
kubectl get pod -l app=tagged -o jsonpath='{.items[0].metadata.annotations}{"\n"}'
```
</details>

**Cleanup:** `kubectl delete -f tagged.yaml`

---

## Section F — Operational Skills

### Drill 58 — `kubectl run` with `--command`
**Curriculum:** Application Observability & Maintenance
**Budget:** 2 min
**Task:** One-shot Pod that runs `cat /etc/os-release` inside `busybox:1.36` and exits. Use `kubectl run` only — no manifest file. Auto-clean.

<details><summary>Answer</summary>

```bash
kubectl run os --rm -it --restart=Never \
  --image=busybox:1.36 \
  --command -- cat /etc/os-release
```

The `--command` flag tells `kubectl run` that everything after `--` is the container's `command`, not its `args`.
</details>

**Cleanup:** none (`--rm`).

---

### Drill 59 — `kubectl cp` files in and out
**Curriculum:** Application Observability & Maintenance
**Budget:** 3 min
**Task:** Copy `/etc/hostname` from the Pod `flow-xxxx` (any pod from drill 56, or create a new busybox sleeper) to a local file `pod-hostname.txt`. Then copy a local file `note.txt` (with text `hi`) into the Pod at `/tmp/note.txt`.

<details><summary>Answer</summary>

```bash
kubectl run keeper --image=busybox:1.36 --restart=Never -- sleep 3600
kubectl wait --for=condition=Ready pod/keeper --timeout=30s

kubectl cp keeper:/etc/hostname pod-hostname.txt
cat pod-hostname.txt

echo hi > note.txt
kubectl cp note.txt keeper:/tmp/note.txt
kubectl exec keeper -- cat /tmp/note.txt
```
</details>

**Cleanup:** `kubectl delete pod keeper; rm pod-hostname.txt note.txt`

---

### Drill 60 — `kubectl logs` flags
**Curriculum:** Application Observability & Maintenance
**Budget:** 2 min
**Task:** Given a Deployment `noisy` running `busybox:1.36` with command `sh -c "while true; do echo line-$RANDOM; sleep 1; done"`, fetch:
1. The last 5 lines from any one Pod.
2. Logs from **all** Pods of the Deployment, prefixed with the Pod name.
3. Live-tail logs across the Deployment for 10 seconds, then exit.

<details><summary>Answer</summary>

```bash
kubectl create deploy noisy --image=busybox:1.36 --replicas=3 -- \
  sh -c "while true; do echo line-\$RANDOM; sleep 1; done"
kubectl wait --for=condition=Available deploy/noisy --timeout=60s
```

```bash
# 1. Last 5 lines from one Pod
POD=$(kubectl get pod -l app=noisy -o jsonpath='{.items[0].metadata.name}')
kubectl logs "$POD" --tail=5

# 2. All Pods of the Deployment, prefixed with pod/container name
kubectl logs -l app=noisy --prefix --max-log-requests=10

# 3. Live-tail across all Pods for 10s
timeout 10 kubectl logs -f -l app=noisy --prefix --max-log-requests=10 || true
```

Key flags: `-l <selector>` fans out across matching Pods; `--prefix` stamps each line with `[pod/<name> <container>]`; `--max-log-requests` raises the default cap of 5 concurrent streams. (`kubectl logs deploy/noisy` only pulls from a single Pod, which is why the selector form is needed for fan-out.)
</details>

**Cleanup:** `kubectl delete deploy noisy`

---

### Drill 61 — `kubectl wait` patterns
**Curriculum:** Application Observability & Maintenance
**Budget:** 3 min
**Task:** Wait for:
1. Deployment `api` to be `Available` (timeout 60 s).
2. A specific Pod `task14` to be `Ready` (timeout 30 s).
3. A Job `oneshot` to reach condition `complete` (timeout 2 min).

Practise the canonical syntax for each.

<details><summary>Answer</summary>

```bash
# 1. Deployment available
kubectl wait --for=condition=Available deploy/api --timeout=60s

# 2. Pod ready
kubectl wait --for=condition=Ready pod/task14 --timeout=30s

# 3. Job complete (note: lowercase 'complete')
kubectl wait --for=condition=complete job/oneshot --timeout=2m
```

`--for=delete` is also useful — waits for an object to disappear:

```bash
kubectl delete pod task14
kubectl wait --for=delete pod/task14 --timeout=30s
```
</details>

**Cleanup:** none (commands only).

---

## Cleanup (full reset)

```bash
kubectl delete deploy,svc,pod,cm,secret --all -n practice
```

---

## Coverage rationale

| Drill | Curriculum domain | Why it's not in drills 1–4 |
|-------|-------------------|----------------------------|
| 50 | Design & Build | `kubectl explain` flow only mentioned in passing |
| 51 | Design & Build | exec-form `command`/`args` distinction not isolated |
| 52 | Env, Config, Security | downwardAPI volume not previously drilled |
| 53 | Env, Config, Security | `subPath` mount only mentioned in core drills |
| 54 | Services & Networking | cross-namespace DNS resolution |
| 55 | Services & Networking | `ExternalName` Service type |
| 56 | App Deployment | `rollout pause`/`resume` |
| 57 | App Deployment | annotations as metadata-only |
| 58 | Observability | `--command` flag of `kubectl run` |
| 59 | Observability | `kubectl cp` |
| 60 | Observability | `-l selector`, `--prefix`, `--max-log-requests` log flags |
| 61 | Observability | `kubectl wait` condition vocabulary |

## Scoring

Same as the other drill files: 2 pts solved in budget without peeking, 1 pt solved within 1.5× or after one peek, 0 pts otherwise. Target **18 / 24** across this set.
