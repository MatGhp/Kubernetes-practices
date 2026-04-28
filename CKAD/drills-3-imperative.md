# CKAD Imperative `kubectl` Drills

A verb-focused drill sheet. Each drill targets **one imperative `kubectl` command shape** that pays off on the exam — fast resource creation without writing YAML from scratch.

> Companion to the scenario-based drills:
> - [drills-1-core.md](drills-1-core.md) — 25 build-a-thing drills.
> - [drills-2-advanced.md](drills-2-advanced.md) — 12 advanced drills (SecurityContext, ServiceAccount, Ingress, Observability, multi-container, `kubectl edit`).

Run all drills in a reset Minikube profile. See [README.md §5.3](README.md#53-minikube-reset-loop).

Assumed setup before any drill (full block in [README.md §2.1](README.md#21-the-block)):

```bash
kubectl create namespace practice
kubectl config set-context --current --namespace=practice

alias k=kubectl
export do="--dry-run=client -o yaml"
export now="--grace-period=0 --force"

source <(kubectl completion bash)
complete -F __start_kubectl k
```

All answers assume the current namespace is `practice`.

---

## How to use this lab

1. Pick **5 drills** at random.
2. Set a **timer** per drill (see budgets — most are 1–2 min).
3. Solve **without** opening the answer. Allowed help: `kubectl --help`, `kubectl explain`, [kubernetes.io/docs](https://kubernetes.io/docs/).
4. Only expand the answer after you finish **or** time runs out.
5. Log each miss (`drill`, `mistake`, `faster command`).

The point is **muscle memory**: when the exam asks for a Job that runs `perl -Mbignum...`, your fingers should already be typing `kubectl create job` before you re-read the question.

---

## Section A — Pods & Deployments

### Drill 1 — Run a one-off Pod
**Budget:** 1 min
**Task:** Create Pod `nginx` from image `nginx:1.27`, restart policy `Never`.

<details><summary>Answer</summary>

```bash
kubectl run nginx --image=nginx:1.27 --restart=Never
```

Verify — `READY 1/1`, `STATUS Running`:

```bash
kubectl get pod nginx
```
</details>

---

### Drill 2 — Pod that runs a custom command
**Budget:** 2 min
**Task:** Create Pod `busybox` from `busybox` that runs `sleep 3600`. Anything after `--` is the command.

<details><summary>Answer</summary>

```bash
kubectl run busybox --image=busybox --restart=Never --command -- sleep 3600
```

> Why `--command`: without it, args go to the image's existing `ENTRYPOINT`. With `--command`, the args **replace** the entrypoint (matches Pod spec `command:`).

Verify:

```bash
kubectl get pod busybox -o jsonpath='{.spec.containers[0].command}'   # ["sleep","3600"]
```
</details>

---

### Drill 3 — Pod with port and labels
**Budget:** 2 min
**Task:** Create Pod `web` (`nginx`) exposing port 80, labels `app=web,tier=frontend`.

<details><summary>Answer</summary>

```bash
kubectl run web --image=nginx --port=80 --labels=app=web,tier=frontend
```

Verify:

```bash
kubectl get pod web --show-labels
```
</details>

---

### Drill 4 — Create a Deployment
**Budget:** 1 min
**Task:** Deployment `api` from `nginx:1.27`, **3 replicas**.

<details><summary>Answer</summary>

```bash
kubectl create deployment api --image=nginx:1.27 --replicas=3
```

Verify — `READY 3/3`:

```bash
kubectl get deploy api
```
</details>

---

### Drill 5 — Generate Deployment YAML, don't apply
**Budget:** 2 min
**Task:** Produce a Deployment manifest for `api` (`nginx:1.27`, 2 replicas) into `api.yaml` **without** touching the cluster.

<details><summary>Answer</summary>

```bash
kubectl create deployment api --image=nginx:1.27 --replicas=2 $do > api.yaml
```

Verify:

```bash
head -20 api.yaml          # should start with apiVersion: apps/v1
kubectl apply --dry-run=client -f api.yaml
```
</details>

---

## Section B — Scale, expose, autoscale, rollout

### Drill 6 — Scale a Deployment
**Budget:** 1 min
**Task:** Scale `api` to 5 replicas.

<details><summary>Answer</summary>

```bash
kubectl scale deployment api --replicas=5
```

Verify:

```bash
kubectl get deploy api    # READY 5/5
```
</details>

---

### Drill 7 — Expose a Deployment as a Service
**Budget:** 2 min
**Task:** Expose Deployment `api` as `ClusterIP` Service `api-svc` on port 80, target port 8080.

<details><summary>Answer</summary>

```bash
kubectl expose deployment api --name=api-svc --port=80 --target-port=8080
```

Verify:

```bash
kubectl get svc api-svc -o wide
kubectl get endpoints api-svc      # endpoints == api pod IPs
```
</details>

---

### Drill 8 — Expose a Pod as NodePort
**Budget:** 2 min
**Task:** Expose Pod `web` as a NodePort Service named `web-np` on port 80.

<details><summary>Answer</summary>

```bash
kubectl expose pod web --name=web-np --type=NodePort --port=80
```

Verify — note the `NODE-PORT` column:

```bash
kubectl get svc web-np
```
</details>

---

### Drill 9 — Autoscale a Deployment (HPA)
**Budget:** 2 min
**Task:** Create an HPA for `api`: min 2, max 5, target CPU 70%.

<details><summary>Answer</summary>

```bash
kubectl autoscale deployment api --min=2 --max=5 --cpu-percent=70
```

Verify:

```bash
kubectl get hpa api
```

> `kubectl top` and the HPA need `metrics-server`. See [README §9.4](README.md#94-metrics-server--kubectl-top-drill-33).
</details>

---

### Drill 10 — Rollout: status, history, undo, restart
**Budget:** 3 min
**Task:** For Deployment `api`:
1. update its image to `nginx:1.28`,
2. watch the rollout finish,
3. show the rollout history,
4. roll back to the previous revision,
5. restart all pods (rolling).

<details><summary>Answer</summary>

```bash
kubectl set image deploy/api nginx=nginx:1.28
kubectl rollout status deploy/api
kubectl rollout history deploy/api
kubectl rollout undo deploy/api
kubectl rollout restart deploy/api
```

Verify the active image after undo (should be `nginx:1.27`):

```bash
kubectl get deploy api -o jsonpath='{.spec.template.spec.containers[0].image}'
```

> `set image deploy/<name> <container>=<image>`. The container name comes from the Pod template — for a freshly created Deployment it usually matches the deployment name (`nginx` here, matching `--image=nginx:1.27` from drill 4).
</details>

---

### Drill 11 — Set environment variables on a Deployment
**Budget:** 2 min
**Task:** Set `LOG_LEVEL=debug` on Deployment `api` (triggers a rollout).

<details><summary>Answer</summary>

```bash
kubectl set env deploy/api LOG_LEVEL=debug
```

Verify:

```bash
kubectl set env deploy/api --list
kubectl rollout status deploy/api
```

> Also: `kubectl set env deploy/api --from=configmap/app-cfg` to inject every key in a ConfigMap as an env var.
</details>

---

## Section C — ConfigMaps & Secrets

### Drill 12 — ConfigMap from literals
**Budget:** 1 min
**Task:** ConfigMap `app-cfg` with keys `ENV=prod`, `LOG=debug`.

<details><summary>Answer</summary>

```bash
kubectl create cm app-cfg --from-literal=ENV=prod --from-literal=LOG=debug
```

Verify:

```bash
kubectl get cm app-cfg -o jsonpath='{.data}'
```
</details>

---

### Drill 13 — ConfigMap from a file or env-file
**Budget:** 3 min
**Task:**
1. Create file `app.env` with two lines `ENV=prod` and `LOG=debug`.
2. Make ConfigMap `app-env` from `app.env` so each line becomes a separate key (`ENV`, `LOG`).
3. Make ConfigMap `app-cfg-file` from the **whole file** so the key is `app.env` and the value is the file's contents.

<details><summary>Answer</summary>

```bash
printf 'ENV=prod\nLOG=debug\n' > app.env

# Each line → its own key:
kubectl create cm app-env --from-env-file=app.env

# Whole file → one key (the filename):
kubectl create cm app-cfg-file --from-file=app.env
```

Verify:

```bash
kubectl get cm app-env       -o jsonpath='{.data}'   # keys: ENV, LOG
kubectl get cm app-cfg-file  -o jsonpath='{.data}'   # key:  app.env
```

> `--from-env-file` parses lines. `--from-file` stores the file. Two **different** outcomes — make sure you reach for the right flag.
</details>

---

### Drill 14 — Generic Secret from literals
**Budget:** 1 min
**Task:** Secret `db` with `user=admin`, `pass=s3cr3t`.

<details><summary>Answer</summary>

```bash
kubectl create secret generic db --from-literal=user=admin --from-literal=pass=s3cr3t
```

Verify (values are base64-encoded):

```bash
kubectl get secret db -o jsonpath='{.data.user}' | base64 -d   # admin
```
</details>

---

### Drill 15 — Docker registry pull Secret
**Budget:** 2 min
**Task:** Secret `regcred` for `myregistry.example.com`, user `bob`, password `hunter2`.

<details><summary>Answer</summary>

```bash
kubectl create secret docker-registry regcred \
  --docker-server=myregistry.example.com \
  --docker-username=bob \
  --docker-password=hunter2 \
  [email protected]
```

Verify — type must be `kubernetes.io/dockerconfigjson`:

```bash
kubectl get secret regcred -o jsonpath='{.type}'
```

> Reference it from a Pod with `spec.imagePullSecrets: [{name: regcred}]`.
</details>

---

### Drill 16 — TLS Secret from cert+key files
**Budget:** 2 min
**Task:** Given files `tls.crt` and `tls.key` exist in the current directory, create Secret `web-tls` of type TLS.

<details><summary>Answer</summary>

```bash
kubectl create secret tls web-tls --cert=tls.crt --key=tls.key
```

Verify — type must be `kubernetes.io/tls`:

```bash
kubectl get secret web-tls -o jsonpath='{.type}'
```
</details>

---

## Section D — Jobs, CronJobs, ServiceAccounts

### Drill 17 — Create a Job
**Budget:** 2 min
**Task:** Job `pi` that prints 50 digits of pi using image `perl`.

<details><summary>Answer</summary>

```bash
kubectl create job pi --image=perl -- perl -Mbignum=bpi -wle 'print bpi(50)'
```

Verify:

```bash
kubectl wait --for=condition=Complete job/pi --timeout=60s
kubectl logs job/pi
```
</details>

---

### Drill 18 — Create a CronJob
**Budget:** 2 min
**Task:** CronJob `hello` (image `busybox`) that prints `hello` every 5 minutes.

<details><summary>Answer</summary>

```bash
kubectl create cronjob hello --image=busybox --schedule="*/5 * * * *" -- echo hello
```

Verify:

```bash
kubectl get cronjob hello
```

> Tip: `--schedule="* * * * *"` (every minute) is convenient when practicing — you don't have to wait 5 minutes for the first run.
</details>

---

### Drill 19 — ServiceAccount and short-lived token
**Budget:** 2 min
**Task:** Create ServiceAccount `build-sa`, then mint a short-lived token for it (modern alternative to legacy `kubernetes.io/service-account-token` Secrets).

<details><summary>Answer</summary>

```bash
kubectl create serviceaccount build-sa
kubectl create token build-sa --duration=1h
```

Verify:

```bash
kubectl get sa build-sa
```

> See [demos/01-configuration/README.md](../demos/01-configuration/README.md) for the full SA story (12-1 basic / 12-2 legacy / 12-3 modern projected token).
</details>

---

## Section E — RBAC

### Drill 20 — Role + RoleBinding
**Budget:** 3 min
**Task:**
1. Role `pod-reader` allowing `get,list,watch` on `pods`.
2. RoleBinding `read-pods` binding `pod-reader` to ServiceAccount `practice:build-sa`.
3. Verify with `kubectl auth can-i`.

<details><summary>Answer</summary>

```bash
kubectl create role pod-reader --verb=get,list,watch --resource=pods
kubectl create rolebinding read-pods --role=pod-reader --serviceaccount=practice:build-sa
```

Verify — both should print `yes` / `no` as expected:

```bash
kubectl auth can-i list   pods --as=system:serviceaccount:practice:build-sa   # yes
kubectl auth can-i delete pods --as=system:serviceaccount:practice:build-sa   # no
```
</details>

---

### Drill 21 — ClusterRole + ClusterRoleBinding
**Budget:** 2 min
**Task:** ClusterRole `node-reader` (`get,list` on `nodes`), bound cluster-wide to `practice:build-sa`.

<details><summary>Answer</summary>

```bash
kubectl create clusterrole node-reader --verb=get,list --resource=nodes
kubectl create clusterrolebinding read-nodes \
  --clusterrole=node-reader --serviceaccount=practice:build-sa
```

Verify (cluster scope, no namespace):

```bash
kubectl auth can-i list nodes --as=system:serviceaccount:practice:build-sa   # yes
```
</details>

---

### Drill 22 — Role for a non-resource URL
**Budget:** 2 min
**Task:** ClusterRole `metrics-reader` allowing `get` on the non-resource URL `/metrics`.

<details><summary>Answer</summary>

```bash
kubectl create clusterrole metrics-reader --verb=get --non-resource-url=/metrics
```

Verify:

```bash
kubectl get clusterrole metrics-reader -o yaml | grep -A3 nonResourceURLs
```
</details>

---

## Section F — Ingress, Quotas, NetworkPolicy

### Drill 23 — Path- and host-based Ingress
**Budget:** 3 min
**Task:** Ingress `web-ing` with class `nginx`, host `web.local`, path `/` → Service `web:80`.

<details><summary>Answer</summary>

```bash
kubectl create ingress web-ing \
  --class=nginx \
  --rule="web.local/*=web:80"
```

Variants worth memorising:

```bash
# Path-only (no host):
--rule="/=web:80"

# Multiple paths:
--rule="/=web:80" --rule="/api=api:80"

# TLS:
--rule="web.local/*=web:80,tls=web-tls"
```

Verify:

```bash
kubectl describe ingress web-ing | grep -E 'Class|Host|Path|Backend'
```

> Full Ingress practice: [drills-2-advanced.md Section H](drills-2-advanced.md#section-h--ingress-networkingk8siov1). Exam-day actions: [README §9.1](README.md#91-ingress-drills-3132).
</details>

---

### Drill 24 — ResourceQuota
**Budget:** 2 min
**Task:** Quota `team-q` capping namespace `practice` at 2 CPU, 4Gi memory, 10 pods.

<details><summary>Answer</summary>

```bash
kubectl create quota team-q --hard=cpu=2,memory=4Gi,pods=10
```

Verify:

```bash
kubectl describe quota team-q
```
</details>

---

### Drill 25 — NetworkPolicy (no imperative form)
**Budget:** 3 min
**Task:** There is **no** `kubectl create networkpolicy` command. Practice the 30-second YAML scaffold instead: deny-all-ingress for the namespace.

<details><summary>Answer</summary>

```bash
cat <<'EOF' | kubectl apply -f -
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: default-deny-ingress
spec:
  podSelector: {}
  policyTypes: [Ingress]
EOF
```

Verify:

```bash
kubectl get netpol default-deny-ingress
```

> Memorise this skeleton — `podSelector: {}` selects everything, `policyTypes: [Ingress]` with no `ingress:` rules = deny-all. Full NetworkPolicy practice: [drills-1-core.md Drill 23](drills-1-core.md#drill-23--networkpolicy--allow-only-from-labeled-pods). Exam-day actions: [README §9.2](README.md#92-networkpolicy-drill-23).
</details>

---

## Section G — Labels, annotations, taints, edit-in-place

### Drill 26 — Label and annotate
**Budget:** 2 min
**Task:**
1. Add label `tier=frontend` to Pod `web` (overwrite if exists).
2. Add annotation `owner=team-a` to Deployment `api`.
3. Remove label `tier` from Pod `web`.

<details><summary>Answer</summary>

```bash
kubectl label   pod web        tier=frontend --overwrite
kubectl annotate deploy/api    owner=team-a
kubectl label   pod web        tier-                          # trailing dash removes
```

Verify:

```bash
kubectl get pod web --show-labels
kubectl get deploy api -o jsonpath='{.metadata.annotations.owner}'
```
</details>

---

### Drill 27 — Taint and untaint a node
**Budget:** 2 min
**Task:** Taint node `<node>` with `dedicated=ckad:NoSchedule`, then remove it.

<details><summary>Answer</summary>

```bash
NODE=$(kubectl get nodes -o name | head -1)
kubectl taint nodes "$NODE" dedicated=ckad:NoSchedule
kubectl taint nodes "$NODE" dedicated=ckad:NoSchedule-      # trailing dash removes
```

Verify:

```bash
kubectl describe node "$NODE" | grep Taints
```
</details>

---

### Drill 28 — Patch and edit in place
**Budget:** 2 min
**Task:**
1. Use `patch` (not `edit`) to change `api` to 4 replicas.
2. Then use `edit` to switch its image to `nginx:1.29` (just demonstrate the workflow).

<details><summary>Answer</summary>

```bash
kubectl patch deploy/api -p '{"spec":{"replicas":4}}'
kubectl edit  deploy/api    # change image, :wq
```

Verify:

```bash
kubectl get deploy api -o jsonpath='{.spec.replicas}{"\n"}{.spec.template.spec.containers[0].image}'
```

> `patch` is scriptable and survives YAML drift. `edit` is faster when you need to touch several fields at once. The exam allows both — use whichever is faster for the question.
</details>

---

## Section H — Debug / inspect / scaffolds

These four are pure speed multipliers — practice until they're reflex.

### Drill 29 — Throwaway debug Pod
**Budget:** 1 min
**Task:** Open an interactive `sh` in a one-shot `busybox` Pod and have it self-delete on exit.

<details><summary>Answer</summary>

```bash
kubectl run tmp --rm -it --image=busybox --restart=Never -- sh
```

Inside, typical checks:

```sh
nslookup api-svc
wget -qO- --timeout=2 http://api-svc:80
exit
```

> `--rm` only works with `--restart=Never` and `-it`. Memorise the full incantation.
</details>

---

### Drill 30 — Dump a live object as a YAML scaffold
**Budget:** 2 min
**Task:** Save Deployment `api`'s spec to `api-live.yaml` and remove the server-side fields you should never re-apply.

<details><summary>Answer</summary>

```bash
kubectl get deploy api -o yaml > api-live.yaml
# Then in vim, delete: status:, metadata.uid, metadata.resourceVersion,
# metadata.creationTimestamp, metadata.generation, metadata.managedFields,
# spec.template.metadata.creationTimestamp.
```

Verify the cleaned file applies cleanly:

```bash
kubectl apply --dry-run=client -f api-live.yaml
```

> Faster alternative when you only need the **template**: regenerate with `kubectl create deployment ... $do > api.yaml` (drill 5). Use the live-dump approach when the object has been customised in ways `create` can't reproduce.
</details>

---

### Drill 31 — Find a field path with `kubectl explain`
**Budget:** 2 min
**Task:** Without opening the docs, find the YAML path for a container's CPU/memory **limits**.

<details><summary>Answer</summary>

```bash
kubectl explain pod.spec.containers.resources
kubectl explain pod.spec.containers.resources.limits
```

Expected path: `spec.containers[].resources.limits.{cpu,memory}`.

> `--recursive` dumps the whole subtree at once: `kubectl explain pod.spec.containers --recursive | less`.
</details>

---

### Drill 32 — `auth can-i`, `api-resources`, `api-versions`
**Budget:** 2 min
**Task:**
1. Can `practice:build-sa` create deployments? (expect `no`)
2. List every namespaced resource and its short name.
3. Confirm `networking.k8s.io/v1` is served by the cluster.

<details><summary>Answer</summary>

```bash
kubectl auth can-i create deployments \
  --as=system:serviceaccount:practice:build-sa --namespace=practice
kubectl api-resources --namespaced=true -o wide
kubectl api-versions | grep '^networking.k8s.io/v1$'
```

> `kubectl api-resources` is the fastest way to remember whether something is `cm`, `pvc`, `pdb`, `netpol`, `sa`, `rb`, `crb`… short names are exam gold.
</details>

---

## Cleanup

```bash
kubectl delete deploy api
kubectl delete pod nginx busybox web --ignore-not-found
kubectl delete svc api-svc web-np --ignore-not-found
kubectl delete cm app-cfg app-env app-cfg-file --ignore-not-found
kubectl delete secret db regcred web-tls --ignore-not-found
kubectl delete job pi --ignore-not-found
kubectl delete cronjob hello --ignore-not-found
kubectl delete sa build-sa --ignore-not-found
kubectl delete role pod-reader --ignore-not-found
kubectl delete rolebinding read-pods --ignore-not-found
kubectl delete clusterrole node-reader metrics-reader --ignore-not-found
kubectl delete clusterrolebinding read-nodes --ignore-not-found
kubectl delete ingress web-ing --ignore-not-found
kubectl delete quota team-q --ignore-not-found
kubectl delete netpol default-deny-ingress --ignore-not-found
kubectl delete hpa api --ignore-not-found
```

Or just nuke the practice namespace and recreate:

```bash
kubectl delete ns practice && kubectl create ns practice
```

---

## Scoring

- Solved within budget, no peek → **2 pts**
- Solved within 1.5× budget, or peeked once → **1 pt**
- Did not solve, or used full answer → **0 pts**

Target for exam-readiness: **52+ / 64** across a full 32-drill run, with **zero misses** in Section H (debug/inspect — these are the time-savers).
