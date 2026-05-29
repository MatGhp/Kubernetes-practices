# Exam Setup (run once at shell start)
```bash
export do="--dry-run=client -o yaml"
alias k=kubectl
```

---

1- Create Deployment:
- k create deploy web --image=nginx:1.27-alpine --replicas=3
- k create deploy web --image=nginx --port=80 --replicas=3

2- Create Pod:
- k run tools --image=busybox:1.36 -- sleep infinity
- k run web --image=nginx --port=80 --expose  # creates Pod + Service

3- Scale deployment:
- k scale deploy/web --replicas=3

4- Update the image:
- k set image deploy/web web=nginx:1.27-alpine  # syntax: CONTAINER_NAME=IMAGE
- k get deploy/web -o jsonpath='{.spec.template.spec.containers[0].name}'  # get container name

Deployment always check:
- k rollout status deploy/web

5- expose deployment
- k expose deploy/web --port=80 --target-port=80    # port=Service port, target-port=container port

6- exec into a pod
- k exec -it tools -- sh   # connect to shell
- k exec tools -- wget -O- http://web   # test connection to web service. -qO- is used to suppress output and print the response to stdout. q==quit after download, O==output to stdout

7- print pod label
- k get pods -l app=web --show-labels



rollout

- k rollout history deploy/web
- k rollout undo deploy/web --to-revision=2
- k rollout undo deploy/web
- k rollout history deploy/web
- k rollout status deploy/web

------------------------

ConfigMap as env vars

Create ConfigMap:
- k create configmap app-cfg --from-literal=APP_ENV=prod --from-literal=APP_TIER=web
- k create configmap app-cfg --from-file=config.properties  # from file

For Deployment (imperative):
- k set env deploy/web --from=configmap/app-cfg              # all keys at once
- k set env deploy/web APP_ENV=prod                           # single key/value
- k get deploy/web -o yaml | grep -A3 env:                    # verify

For Pod (YAML only - no imperative for envFrom):
k run cm-env --image=busybox:1.36 --restart=Never $do -- sh -c 'sleep infinity' > cm-env.yaml
# Edit cm-env.yaml to add envFrom section under containers[0]:
```yaml
  containers:
    - name: app
      ...
      envFrom:
        - configMapRef:
            name: app-cfg      # exposes ALL keys as env vars
```

```yaml
  containers:
    - name: app
      ...
      - name: APP_ENV
        valueFrom:
          configMapKeyRef:
            name: app-cfg
            key: APP_ENV       # exposes a single key as env var
```
- k apply -f cm-env.yaml
- k exec cm-env -- env | grep APP_  # verify
----------------------
ConfigMap as a volume
```yaml
  containers:
    - name: app
      ...
      volumeMounts:
        - name: cfg
          mountPath: /etc/app-cfg
          readOnly: true
```

```yaml
  volumes:
    - name: cfg
      configMap:
        name: app-cfg
```

- kubectl exec cm-vol -- ls /etc/app-cfg           # APP_ENV APP_TIER
- kubectl exec cm-vol -- cat /etc/app-cfg/APP_ENV  # prod
--------------------
Secrets (similar to ConfigMap)

Create Secret:
- k create secret generic db-creds --from-literal=user=admin --from-literal=pass=secret
- k create secret generic db-creds --from-file=credentials.txt  # from file

For Deployment (imperative):
- k set env deploy/web --from=secret/db-creds  # all keys at once
- k set env deploy/web DB_USER=admin  # single key/value

For Pod (YAML only):
k run secret-pod --image=busybox:1.36 --restart=Never $do -- sh -c 'sleep infinity' > secret-pod.yaml
# Edit secret-pod.yaml to add envFrom section under containers[0]:
```yaml
 containers:
    - name: app
      ...
      envFrom:                 # important - envFrom, not env
        - secretRef:           # important - secretRef, not secretKeyRef
            name: db-creds
```

or
```yaml
  containers:
    - name: app
      ...
      env:                       # individual env var from secret
        - name: API_KEY          # env var name in the container
          valueFrom:             # important - valueFrom, not value
            secretKeyRef:        # important - secretKeyRef, not secretRef
              name: app-sec      # 
              key: API_KEY
```

- kubectl apply -f secret-pod.yaml
- kubectl exec secret-pod -- env | grep DB_  # verify
- kubectl exec sec-pod -- sh -c 'echo $API_KEY'
------------------


Labels & Annotations

- k label pod web tier=frontend
- k label pod web tier=backend --overwrite  # change existing label
- k get pods -l tier=frontend  # filter by label
- k annotate pod web description="web server"
- k annotate pod web description-  # remove annotation



Delete Resources

- k delete pod web
- k delete pod web --force --grace-period=0  # fast delete for exam
- k delete deploy web
- k delete svc web
- k delete all --all  # delete all resources in current namespace

-----------------

readinessProbe httpGet / port 80 (delay 2s, period 5s)
livenessProbe httpGet / port 80 (delay 10s, period 10s)

```yaml
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
--------------------

Pod
requests cpu=100m, memory=128Mi and limits cpu=250m, memory=256Mi

```yaml
      resources:
        requests:
          cpu: "100m"
          memory: "128Mi"
        limits:
          cpu: "250m"
          memory: "256Mi"
```
-------------------

#sidecar Pod
```yaml

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
          mountPath: /var/log  #important
    - name: sidecar
      image: busybox
      command: ["sh", "-c", "tail -F /var/log/app.log"]
      volumeMounts:
        - name: logs
          mountPath: /var/log #important
```
---------------------
NetworkPolicy:
```yaml
spec:
  podSelector:
    matchLabels:             # select the Pods this policy applies to (the "app" Pods)
      app: web
  policyTypes: ["Ingress"]
  ingress:
    - from:
        - podSelector:
            matchLabels:     # select the source Pods allowed to connect (the "client" Pods)
              role: client
      ports:                 # select the ports allowed (only TCP 80)
        - protocol: TCP
          port: 80
```
------------------------------

port-forwarding

kubectl port-forward svc/<service-name> <local-port>:<service-port>

kubectl port-forward svc/web 8080:80 &  # & -> run in background
> then you can run  -> curl http://localhost:8080
> kill %1  # stop port-forwarding
----------------

```yaml
#PVC
#pod
spec:
  volumes:
    - name: data
      persistentVolumeClaim: # here
        claimName: data-pvc  # here
  containers:
    - name: app
      image: busybox
      volumeMounts:
        - name: data # here
          mountPath: /data
```

------------------
hpa

Create an HPA for api: min 2, max 5, target CPU 70%.

kubectl autoscale deployment api --min=2 --max=5 --cpu=70%
kubectl get hpa api
----------------

Set LOG_LEVEL=debug on Deployment api (triggers a rollout).

```yaml
kubectl set env deploy/api LOG_LEVEL=debug

kubectl set env deploy/api --list      # shows all env vars, including the new LOG_LEVEL=debug
kubectl rollout status deploy/api

kubectl set env deploy/api --from=configmap/app-cfg  # inject every key in a ConfigMap as an env var



kubectl set env deploy/api --from=configmap/app-cfg   # inject ALL keys from a ConfigMap as env vars
kubectl set env deploy/api --from=secret/db-creds      # inject ALL keys from a Secret as env vars
kubectl set image deploy/api api=nginx:1.28             # update container image
kubectl set resources deploy/api -c api --requests=cpu=100m,memory=128Mi --limits=cpu=250m,memory=256Mi
kubectl set serviceaccount deploy/api build-sa          # assign a ServiceAccount

# --from=SOURCE/NAME is ONLY supported by `set env` (source must be configmap or secret)
# `set image`, `set resources`, `set serviceaccount` do NOT support --from

# Works on:   deploy, ds, sts, rs, rc, (job/cronjob for env & image)
# Does NOT work on: pod for `set resources` and `set serviceaccount`
#   → pods are mostly immutable after creation; use deploy/ds/sts instead
```
---


Create file app.env with two lines ENV=prod and LOG=debug.
```yaml
printf 'ENV=prod\nLOG=debug\n' > app.env
```

Make ConfigMap app-env from app.env so each line becomes a separate key (ENV, LOG).
```yaml
kubectl create cm app-env --from-env-file=app.env
```

Make ConfigMap app-cfg-file from the whole file so the key is app.env and the value is the file's contents.
```yaml
kubectl create cm app-cfg-file --from-file=app.env
```
---------

Create ServiceAccount build-sa, then mint a short-lived token for it
```yaml
kubectl create sa build-sa
kubectl create token build-sa --duration=1h  # build-sa is the name of the ServiceAccount
```

---

Namespaces

- k create ns staging
- k config set-context --current --namespace=staging  # pin default namespace for session
- k config view --minify | grep namespace              # verify current namespace
- k get pods -n kube-system                            # query a specific namespace

---

Jobs / CronJobs

- k create job pi --image=perl:5.34 -- perl -Mbignum=bpi -wle 'print bpi(2000)'
- k create cronjob hello --image=busybox --schedule="*/1 * * * *" -- echo hello
- k get jobs
- k logs job/pi
# YAML-only extras: backoffLimit, completions, parallelism, activeDeadlineSeconds

# CronJob logs — CronJob itself has no logs; each run creates a Job → Job creates a Pod
- k get jobs -l cronjob-name=hello          # list jobs spawned by the cronjob
- k get pods -l job-name=<job-name>         # find the pod for that job run
- k logs <pod-name>                         # read the logs
# One-liner: logs of the latest cronjob run
- k logs $(k get pods -l job-name=$(k get job -l cronjob-name=hello --sort-by=.metadata.creationTimestamp -o name | tail -1 | cut -d/ -f2) -o name | head -1)

---

RBAC

- k create role pod-reader --verb=get,list,watch --resource=pods
- k create rolebinding read-pods --role=pod-reader --serviceaccount=default:build-sa
- k auth can-i get pods --as=system:serviceaccount:default:build-sa  # verify

```yaml
# Role + RoleBinding (namespace-scoped)
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: pod-reader
  namespace: default
rules:
  - apiGroups: [""]
    resources: ["pods"]
    verbs: ["get", "list", "watch"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: read-pods
  namespace: default
subjects:
  - kind: ServiceAccount
    name: build-sa
    namespace: default
roleRef:
  kind: Role
  name: pod-reader
  apiGroup: rbac.authorization.k8s.io
```

```yaml
# ClusterRole + ClusterRoleBinding (cluster-wide)
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: pod-reader
rules:
  - apiGroups: [""]
    resources: ["pods"]
    verbs: ["get", "list", "watch"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: read-pods-global
subjects:
  - kind: ServiceAccount
    name: build-sa
    namespace: default
roleRef:
  kind: ClusterRole
  name: pod-reader
  apiGroup: rbac.authorization.k8s.io
```

---
Ingress `web-ing` with class `nginx`, host `web.local`, path `/` → Service `web:80`.

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


------------------
Quota `team-q` capping namespace `practice` at 2 CPU, 4Gi memory, 10 pods.

<details><summary>Answer</summary>

```bash
kubectl create quota team-q --hard=cpu=2,memory=4Gi,pods=10
```
--------------------
Open an interactive `sh` in a one-shot `busybox` Pod and have it self-delete on exit.

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
-------------------
1. List all available contexts.
2. Switch to context `ckad` (assuming it exists).
3. Pin the default namespace to `practice` without editing a YAML file.
4. Confirm the active context and namespace.

<details><summary>Answer</summary>

```bash
kubectl config get-contexts
kubectl config use-context ckad
kubectl config set-context --current --namespace=practice   # important --current
kubectl config current-context
kubectl config view --minify | grep namespace
```

> On the real exam, the question header gives you the context (`kubectl config use-context k8s`) and often a namespace. Run both commands before touching anything else.
</details>

------------------------
Service `api-svc` (ClusterIP, port 80) exists in namespace `practice` from Drill 7.
1. Resolve `api-svc` by its **short name** from a throwaway Pod in the same namespace.
2. Resolve it by its **FQDN** from a throwaway Pod in a different namespace `other`.

<details><summary>Answer</summary>

```bash
# Short name - works only within the same namespace
kubectl run probe --rm -it --restart=Never --image=busybox:1.36 -- \
  nslookup api-svc

# FQDN - works from any namespace
kubectl create ns other --dry-run=client -o yaml | kubectl apply -f -
kubectl run probe -n other --rm -it --restart=Never --image=busybox:1.36 -- \
  nslookup api-svc.practice.svc.cluster.local
```

> Short names are resolved by appending the Pod's search domains (`practice.svc.cluster.local`, `svc.cluster.local`, `cluster.local`). A Pod in `other` has different search domains, so `api-svc` alone fails. The FQDN always works.
>
> FQDN pattern: `<service>.<namespace>.svc.cluster.local`
</details>

--------
Service `api-svc` (ClusterIP, port 80) exists in namespace `practice`.
1. Send an HTTP GET to `api-svc:80` from a throwaway Pod and print the full response body.
2. Print only the HTTP status line (no body) using a `curl`-capable image.

<details><summary>Answer</summary>

```bash
# wget - busybox has it built in, no extra image needed
kubectl run probe --rm -it --restart=Never --image=busybox:1.36 -- \
  wget -qO- --timeout=2 http://api-svc:80

# curl - needs curlimages/curl (busybox has no curl)
kubectl run probe --rm -i --restart=Never --image=curlimages/curl -- \
  curl -sI http://api-svc:80 | head -1
```


> `wget -qO- --timeout=2` is the exam workhorse: `-q` suppresses progress noise, `-O-` sends output to stdout, `--timeout=2` prevents hanging on a broken Service.
>
> Use `curlimages/curl` only when you need curl-specific flags (e.g. `-H`, `--resolve`, `-d`). For a simple reachability check, busybox wget is faster to type.
</details>


----------------



k explain (YAML field lookup during exam)

- k explain pod.spec.containers
- k explain pod.spec.containers.resources --recursive
- k explain deployment.spec.strategy

--------------
Pod `sec-uid` (image `busybox`, sleeps forever) that runs as UID `1000` and **must** fail to start if the image tries to run as root.

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
    runAsNonRoot: true    # important - prevents running as root
  containers:
    - name: app
      image: busybox
      command: ["sh", "-c", "id && sleep infinity"]
```
-------------
Pod `sec-ro` (image `busybox`):
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

Verify - logs show `blocked: read-only fs`; `/tmp/out` exists and contains `ok`:

```bash
kubectl logs sec-ro
kubectl exec sec-ro -- cat /tmp/out
```
</details>

----------
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

Verify - jsonpath prints exactly `deployer`; the projected token exists under `/var/run/secrets/kubernetes.io/serviceaccount/`:

```bash
kubectl get pod sa-pod -o jsonpath='{.spec.serviceAccountName}{"\n"}'
kubectl exec sa-pod -- ls /var/run/secrets/kubernetes.io/serviceaccount
```
</details>

---
Give `deployer` permission to `get` and `list` pods in `practice` (nothing else). From inside `sa-pod`, confirm it can list pods but **cannot** delete them.

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

Verify with `kubectl auth can-i` impersonating the SA - expect `yes` / `no`:

```bash
kubectl auth can-i list pods   --as=system:serviceaccount:practice:deployer -n practice
kubectl auth can-i delete pods --as=system:serviceaccount:practice:deployer -n practice
```
</details>

<details><summary>Alternative (imperative)</summary>

```bash
kubectl create role pod-reader \
  --verb=get,list \
  --resource=pods \
  -n practice

kubectl create rolebinding deployer-can-read-pods \
  --role=pod-reader \
  --serviceaccount=practice:deployer \
  -n practice
```

> `--serviceaccount` takes the format `namespace:name` — the only non-obvious part of the imperative form.

Verify (same as above):

```bash
kubectl auth can-i list pods   --as=system:serviceaccount:practice:deployer -n practice
kubectl auth can-i delete pods --as=system:serviceaccount:practice:deployer -n practice
```
</details>

---

Expose deployment `web` (from part 1) and a new deployment `api` behind one Ingress:
- `GET /`     → service `web` port 80
- `GET /api`  → service `api` port 80 (strip the prefix is not required)

<details><summary>Answer</summary>

Ensure `web` and `api` exist (skip if you already ran the Section H prerequisites block above). See that block for why `args:` + port `5678` are required instead of `kubectl create deployment -- ...`:

```bash
kubectl apply -f - <<'EOF'
apiVersion: apps/v1
kind: Deployment
metadata: { name: api, labels: { app: api } }
spec:
  replicas: 1
  selector: { matchLabels: { app: api } }
  template:
    metadata: { labels: { app: api } }
    spec:
      containers:
        - name: http-echo
          image: hashicorp/http-echo
          args: ["-text=hello from api", "-listen=:5678"]
          ports: [{ containerPort: 5678 }]
---
apiVersion: v1
kind: Service
metadata: { name: api }
spec:
  selector: { app: api }
  ports: [{ port: 80, targetPort: 5678 }]
EOF
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

Verify - `get ingress` shows a non-empty `ADDRESS`; curl returns the nginx welcome page for `/` and `hello from api` for `/api`:

```bash
kubectl get ingress app
IP=$(minikube -p ckad ip)
curl -s http://$IP/       | head -n 1
curl -s http://$IP/api
```
</details>

<details><summary>Alternative (imperative)</summary>

```bash
kubectl create ingress app \
  --class=nginx \
  --rule="/api*=api:80" \
  --rule="/*=web:80"
```

> **Three things to remember:**
> - `*` suffix → `pathType: Prefix`; without it you get `ImplementationSpecific`
> - More specific rule (`/api*`) must come **first** — first-match-wins per the K8s spec (nginx does longest-match in practice, but order matters for correctness)
> - `--class=nginx` matches the minikube addon; on the exam use whatever class the question specifies

Verify (same as above):

```bash
kubectl get ingress app
IP=$(minikube -p ckad ip)
curl -s http://$IP/       | head -n 1
curl -s http://$IP/api
```
</details>

---

Change the Ingress so that `http://web.local` → `web` and `http://api.local` → `api`.

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

Verify - `describe ingress` lists both hosts; curl with `--resolve` reaches the right backend based on the `Host` header:

```bash
kubectl describe ingress app | grep -E 'Host|Path'
IP=$(minikube -p ckad ip)
curl -s --resolve web.local:80:$IP http://web.local/ | head -n 1
curl -s --resolve api.local:80:$IP http://api.local/
```
</details>

<details><summary>Alternative (imperative)</summary>

```bash
kubectl create ingress app \
  --class=nginx \
  --rule="web.local/*=web:80" \
  --rule="api.local/*=api:80"
```

> **Three things to remember:**
> - Format is `host/path*=service:port` — the host comes **before** the `/`
> - `*` suffix → `pathType: Prefix`; omitting it gives `ImplementationSpecific`
> - Each `--rule` flag becomes one host rule; order doesn't matter for host-based routing

Verify (same as above):

```bash
kubectl describe ingress app | grep -E 'Host|Path'
IP=$(minikube -p ckad ip)
curl -s --resolve web.local:80:$IP http://web.local/ | head -n 1
curl -s --resolve api.local:80:$IP http://api.local/
```
</details>

---

## Multi-container Pod patterns

The CKAD exam expects you to know all four classic patterns. Sidecar is above (search "sidecar Pod"). The rest are below. **Key insight:** the exam provides any non-trivial config (nginx, fluentd, etc.) — you only need to wire the Kubernetes objects.

### Init container

Runs to completion **before** any app container starts. Good for waiting on dependencies, fetching config, running migrations.

```yaml
spec:
  restartPolicy: Never
  initContainers:
    - name: wait-for-db
      image: busybox
      command: ["sh", "-c", "until nc -z db 5432; do echo waiting; sleep 2; done"]
  containers:
    - name: app
      image: nginx:1.27
```

> - `initContainers` is a sibling of `containers`, not nested inside it
> - Multiple init containers run **sequentially**, not in parallel
> - If any init container fails, the Pod restarts it per `restartPolicy`

---

### Ambassador (HTTP proxy)

App talks only to `localhost`; ambassador proxies outward to another Service. Config is mounted via ConfigMap.

```yaml
# Assume ConfigMap `amb-proxy` has key `default.conf` with the nginx server block
spec:
  restartPolicy: Never
  volumes:
    - name: cfg
      configMap: { name: amb-proxy }
  containers:
    - name: app
      image: busybox
      command: ["sh","-c","while true; do wget -qO- http://127.0.0.1:8080; sleep 5; done"]
    - name: ambassador
      image: nginx:1.27
      volumeMounts:
        - name: cfg
          mountPath: /etc/nginx/conf.d   # nginx auto-includes anything here
```

### Ambassador (TCP proxy — `subPath` trick)

For raw TCP (redis, postgres) nginx needs a `stream {}` block, which must replace `/etc/nginx/nginx.conf` entirely. Use **`subPath`** to mount a single ConfigMap key as a file without wiping the directory:

```yaml
    - name: proxy
      image: nginx:1.27
      volumeMounts:
        - name: cfg
          mountPath: /etc/nginx/nginx.conf   # exact file path
          subPath: nginx.conf                # key name from the ConfigMap
```

> **Three things to remember:**
> - App container references `127.0.0.1` / `localhost`, never the upstream Service name
> - HTTP proxy → drop file in `/etc/nginx/conf.d/` (no subPath needed)
> - TCP proxy → replace `/etc/nginx/nginx.conf` with `subPath`

---

### Adapter

Transforms the app's output into a format something else expects (e.g. plain log → Prometheus metrics, app log → structured JSON). Same shape as sidecar — shared `emptyDir` volume — but the adapter container *reformats* the data instead of just shipping it.

```yaml
spec:
  restartPolicy: Never
  volumes:
    - name: logs
      emptyDir: {}
  containers:
    - name: app
      image: busybox
      command: ["sh","-c","while true; do echo \"raw $(date)\" >> /var/log/app.log; sleep 2; done"]
      volumeMounts:
        - { name: logs, mountPath: /var/log }
    - name: adapter
      image: busybox
      command: ["sh","-c","tail -F /var/log/app.log | sed 's/^/json: /'"]
      volumeMounts:
        - { name: logs, mountPath: /var/log }
```

---

### Pattern cheat-sheet

| Pattern | Containers run | Volume | App talks to |
|---|---|---|---|
| **Init** | sequential, before app | optional | n/a |
| **Sidecar** | parallel, alongside app | shared `emptyDir` | its own work (logs, etc.) |
| **Ambassador** | parallel | ConfigMap for proxy config | `localhost:<port>` |
| **Adapter** | parallel | shared `emptyDir` | writes to volume; adapter reads + transforms |

---

