# YAML Format - Reference & Drills

A focused guide to YAML syntax for Kubernetes. Read each section, then attempt the drill. Expand the answer only after you try.

> These drills are not timed - understanding matters more than speed here.

---

## 1. Core Rules

- **Spaces only** - tabs are illegal in YAML
- **Indentation defines structure** - two spaces is the Kubernetes convention
- **Case-sensitive** - `apiVersion` ≠ `ApiVersion`
- **Comments** start with `#` and run to end of line
- **Document separator** `---` splits multiple resources in one file

```yaml
# This is a comment
apiVersion: v1   # inline comment
kind: Pod
---              # second document begins here
apiVersion: v1
kind: Service
```

### Drill 1 - Spot the error
**Task:** Find the syntax error.

```yaml
apiVersion: v1
kind: Pod
metadata:
	name: mypod     # ← tab used for indent
spec:
  containers:
  - name: app
    image: nginx:1.27
```

<details><summary>Answer</summary>

The `name` field is indented with a **tab** (`\t`). YAML forbids tabs. Replace with 2 spaces:

```yaml
metadata:
  name: mypod
```
</details>

---

## 2. Scalar Types (single values)

| Type | Example | Notes |
|---|---|---|
| Plain string | `name: mypod` | No quotes needed unless value is ambiguous |
| Single-quoted | `value: 'no expansion'` | Literal - no escape sequences |
| Double-quoted | `value: "line\n"` | Supports `\n`, `\t`, etc. |
| Integer | `replicas: 3` | |
| Float | `cpu: 0.5` | |
| Boolean | `enabled: true` | Also `false` |
| Null | `value: null` or `value: ~` | |

> **Gotcha - YAML 1.1 vs 1.2:** `kubectl` uses a YAML 1.1 parser (Go's `go-yaml` library). In YAML 1.1, `yes`, `no`, `on`, `off` are treated as booleans. The current YAML spec (1.2) recognises only `true` and `false` as booleans, but Kubernetes tooling has not yet migrated. Quote these values when you mean a string: `value: "yes"`.

> **Gotcha - ambiguous scalar values:** Some values look like plain strings but are parsed as other types. Always quote them:
>
> | Value | Parsed as | Use case in K8s |
> |---|---|---|
> | `0755` | Octal integer `493` | `defaultMode` for ConfigMap/Secret volume mounts |
> | `1e3` | Float `1000.0` | Rarely wanted as a string |
> | `0xDEAD` | Hex integer | Rarely wanted as a string |
>
> Fix: `defaultMode: 0755` → `defaultMode: 0o755` (Go octal literal, accepted by K8s) or quote it: `"0755"`.

> **Gotcha - special characters that force quoting:** A plain scalar must be quoted if it starts with (or contains) any of these characters at a position where YAML assigns them structural meaning:
>
> | Character | Meaning in YAML | Example fix |
> |---|---|---|
> | `:` followed by space | key-value separator | `value: "http://example.com"` |
> | `#` | comment start | `label: "my#tag"` |
> | `{` or `}` | flow mapping delimiters | `value: "{key: val}"` |
> | `[` or `]` | flow sequence delimiters | `value: "[a, b]"` |
> | `&` or `*` | anchor / alias | `value: "&ref"` |
> | `!` | YAML tag | `value: "!important"` |
> | `>` or `\|` | block scalar indicators | cannot appear as a plain value; use quoted form |
>
> **Rule of thumb:** if the value contains `:` followed by a space, or starts with any of `{[!"'|>&*%@,` - quote it.

### Drill 2 - Types
**Task:** What type does YAML assign to each value? Are any dangerous?

```yaml
a: true
b: yes
c: 42
d: "42"
e: null
f: on
```

<details><summary>Answer</summary>

| Key | YAML type | Dangerous? |
|---|---|---|
| `a` | Boolean `true` | No |
| `b` | Boolean `true` (YAML 1.1) | ⚠ Yes - if you meant the string `"yes"` |
| `c` | Integer `42` | No |
| `d` | String `"42"` | No - quotes force string |
| `e` | Null | No |
| `f` | Boolean `true` (YAML 1.1) | ⚠ Yes - if you meant the string `"on"` |

In Kubernetes manifests `yes`/`on` are rare but can cause silent validation errors if a field expects a string.
</details>

---

## 3. Mappings (key-value pairs)

A mapping is an unordered set of key-value pairs. Keys at the same indent level belong to the same mapping.

```yaml
metadata:        # mapping key - value is a nested mapping
  name: mypod
  namespace: default
  labels:        # nested mapping
    app: myapp
    version: v1
```

Kubernetes field order convention: `apiVersion → kind → metadata → spec`.

**Empty and null values** behave differently - this matters in Kubernetes selectors and volume lists:

```yaml
podSelector: {}   # empty mapping - match ALL pods (NetworkPolicy wildcard)
podSelector:      # null (bare key, no value) - K8s treats same as {}, but {} is clearer
volumes: []       # explicitly empty list - overrides any inherited volumes
```

### Drill 3 - Nested mapping
**Task:** Write a `metadata` block for a Pod named `db` in namespace `ns-config` with label `app: database`.

<details><summary>Answer</summary>

```yaml
metadata:
  name: db
  namespace: ns-config
  labels:
    app: database
```
</details>

---

## 4. Sequences (lists)

A sequence is an ordered list. Each item starts with `- `.

```yaml
# Block style - most readable
containers:
  - name: app
    image: nginx:1.27
  - name: sidecar
    image: busybox:1.36
```

The `-` can sit at the same column as the key's content or indented further - both are valid:

```yaml
# Dash-aligned style - dash sits at the same column as the key name
containers:
- name: app          # dash at col 0, aligned with "containers:"
  image: nginx:1.27

# Block-indented style - dash is indented 2 spaces inside the key
containers:
  - name: app        # dash at col 2
    image: nginx:1.27
```

### Drill 4 - Write a sequence
**Task:** Write an `env` list for a container with two variables: `DB_HOST=mysql` and `DB_PORT=3306`.

<details><summary>Answer</summary>

```yaml
env:
  - name: DB_HOST
    value: mysql
  - name: DB_PORT
    value: "3306"   # quoted - prevents YAML parsing as integer
```
</details>

---

## 5. Block vs Flow Style

YAML allows two equivalent representations - **block** (multi-line) and **flow** (inline JSON-like).

```yaml
# Block
metadata:
  name: mypod
  labels:
    app: myapp

# Flow - identical meaning
metadata: { name: mypod, labels: { app: myapp } }
```

```yaml
# Block sequence
policyTypes:
  - Ingress
  - Egress

# Flow sequence - identical meaning
policyTypes: [Ingress, Egress]
```

> **Exam tip:** Flow style is faster to type in `kubectl edit` or when writing one-liners. Kubernetes accepts both everywhere.

### Drill 5 - Convert styles
**Task:** Rewrite this in flow style:

```yaml
selector:
  matchLabels:
    app: frontend
    version: v2
```

<details><summary>Answer</summary>

```yaml
selector: { matchLabels: { app: frontend, version: v2 } }
```
</details>

---

## 6. Sequence-of-Mappings - The Tricky Indent Rule

This is the most common source of Kubernetes YAML errors. When a sequence item is itself a mapping, the `-` introduces the item and the item's keys are indented relative to the `-`.

```yaml
ingress:
- from:                         # "from" is a key on this rule item (the "- " at col 0)
  - podSelector:                # "from" is a list; this is its first item
      matchLabels:
        app: frontend
  ports:                        # "ports" is a sibling key to "from" on the same rule item
  - protocol: TCP
    port: 80
```

**Key question:** Is the thing after the dash a *key* or a *new sequence item*?

- `- podSelector:` → sequence item (a new `from` entry)
- `ports:` → mapping key (no dash → belongs to the parent rule item, not inside `from`)

### Drill 6 - NetworkPolicy indent
**Task:** Fix the indentation. `ports` should allow TCP:80 from `app: frontend` pods only.

```yaml
spec:
  ingress:
  - from:
    - podSelector:
        matchLabels:
          app: frontend
      ports:           # ← is this correct?
      - protocol: TCP
        port: 80
```

<details><summary>Answer</summary>

`ports:` is indented as part of the `podSelector` item - that's wrong. `ports` must be a sibling of `from` on the rule item:

```yaml
spec:
  ingress:
  - from:
    - podSelector:
        matchLabels:
          app: frontend
    ports:             # ← sibling of "from:", same indent
    - protocol: TCP
      port: 80
```

The rule item (`- `) has exactly two keys: `from` and `ports`. Both align at the same column.
</details>

---

## 7. Multi-line Strings

```yaml
# Literal block scalar (|) - preserves newlines
script: |
  #!/bin/bash
  echo "hello"
  exit 0

# Folded block scalar (>) - folds newlines into spaces (good for long prose)
description: >
  This is a long description
  that wraps across lines.
```

```yaml
# Strip modifier (|-) - same as | but removes the trailing newline
# Prefer |- over | for shell scripts in ConfigMaps: a trailing newline can silently break scripts
data:
  script.sh: |-
    #!/bin/bash
    echo "done"     # no trailing newline after this line
```

```yaml
# Keep modifier (|+) - preserves ALL trailing newlines (rarely needed; default | keeps exactly one)
data:
  footer: |+
    line one

    # two trailing newlines preserved
```

| Modifier | Trailing newline behaviour |
|---|---|
| `\|` | Keeps exactly one trailing newline (default) |
| `\|-` | Strips all trailing newlines |
| `\|+` | Keeps all trailing newlines |

> **Exam tip:** Use `|-` (not `|`) for shell script content in ConfigMaps. The trailing newline that `|` preserves can silently break scripts that are sourced or executed directly.

### Drill 7 - Multi-line command
**Task:** Write the `command` and `args` fields for a container that runs two shell commands sequentially using a literal block scalar.

<details><summary>Answer</summary>

```yaml
command: ["/bin/sh", "-c"]
args:
  - |
    echo "starting"
    nginx -g 'daemon off;'
```
</details>

---

## 8. Anchors & Aliases (reuse blocks)

Anchors define a reusable block (`&name`). Aliases paste it back in (`*name`). Anchors are **document-scoped** - an alias cannot reference an anchor defined in a different YAML document (across `---`).

```yaml
# Abstract illustration - not a deployable K8s manifest
labels: &appLabels    # anchor named "appLabels"
  app: myapp
  team: platform

resource:
  metadata:
    labels: *appLabels  # alias - inlines app: myapp and team: platform
```

> Kubernetes itself does not interpret anchors - they are resolved by the YAML parser before the manifest reaches the API server, so `kubectl apply` handles them transparently.

**Merge key (`<<`):** Merges all keys from a referenced mapping into the current mapping. Specific keys can be overridden after the merge:

```yaml
defaults: &defaults
  protocol: TCP
  port: 80
  timeout: 30s

ingress-rule:
  <<: *defaults   # merges protocol, port, timeout
  port: 443       # overrides only port; protocol and timeout are inherited
```

### Drill 8 - Anchors in a Deployment
**Task:** When would anchors be most useful within a single Kubernetes manifest, and what is the key constraint to remember about anchors and `---` document separators?

<details><summary>Answer</summary>

Anchors are useful when the **same label set, selector, or resource limits** appear multiple times in one manifest - e.g., `metadata.labels`, `spec.selector.matchLabels`, and `spec.template.metadata.labels` in a Deployment all need identical values.

**Key constraint:** Anchors are document-scoped. An alias (`*name`) cannot reference an anchor defined in a different document (across `---`). Define anchors in the same document as their aliases.

```yaml
# Anchor defined at the first occurrence of the label set
apiVersion: apps/v1
kind: Deployment
metadata:
  name: myapp
  labels: &appLabels    # anchor defined here
    app: myapp
    version: v1
spec:
  selector:
    matchLabels: *appLabels     # alias - same label set, no copy-paste
  template:
    metadata:
      labels: *appLabels        # alias again
    spec:
      containers:
      - name: app
        image: nginx:1.27
```
</details>

---

## 9. Common Kubernetes YAML Pitfalls

| Pitfall | Symptom / cause | Fixed |
|---|---|---|
| `ports:` inside `from:` (NetworkPolicy) | Parse error or policy silently allows all traffic | `ports:` as sibling of `from:`, same indent |
| `matchLabels` doesn’t match `template.metadata.labels` | Pods never reach `Ready` - selector matches nothing | Labels must be identical in selector and template |
| Integer where string expected | Env var becomes int, not string - app may crash | Wrap in quotes: `value: "8080"` |
| `latest` image tag | Unexpected pulls; `ErrImagePull` in air-gapped clusters | Pin version: `image: nginx:1.27` |
| Missing `---` separator in multi-resource file | Second resource silently dropped or parse error | Add `---` between each resource |
| Tab indentation | `error converting YAML to JSON` parse error | Spaces only |
| `yes`/`no` used as strings | Silently coerced to boolean `true`/`false` | Quote: `value: "yes"` |
| Removed `apiVersion` (e.g. `extensions/v1beta1`) | `no matches for kind ... in version ...` error | Use current stable version: `networking.k8s.io/v1` |

### Drill 9 - Two-bug YAML
**Task:** Find and fix both bugs.

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: web
spec:
  replicas: 2
  selector:
    matchLabels:
      app: web
  template:
    metadata:
      labels:
        app: frontend
    spec:
      containers:
      - name: web
        image: nginx
        resources:
          requests:
            cpu: "100m"
          limits:
            cpu: "200m"
```

<details><summary>Answer</summary>

**Bug 1 - label mismatch:** `selector.matchLabels` is `app: web` but `template.metadata.labels` is `app: frontend`. The Deployment will never match its own pods. Fix:

```yaml
      labels:
        app: web
```

**Bug 2 - `latest` tag:** `image: nginx` implicitly uses `latest`. On exam clusters `imagePullPolicy` defaults to `IfNotPresent` for tagged images, but `latest` always triggers a pull which may fail or be unpredictable. Fix:

```yaml
        image: nginx:1.27
```
</details>

---

## 10. Quick Reference - Kubernetes API Field Types

| Field | YAML type | Example |
|---|---|---|
| `replicas` | integer | `replicas: 3` |
| `image` | string | `image: nginx:1.27` |
| `env` | sequence of mappings | `- {name: VAR, value: val}` |
| `command` / `args` | sequence of strings | `["sh", "-c", "echo hi"]` |
| `labels` / `annotations` | mapping of strings | `app: web` |
| `ports` | sequence of mappings | `- containerPort: 80` |
| `resources.requests` | mapping of quantities | `cpu: "100m", memory: "128Mi"` |
| `podSelector: {}` | empty mapping = select all | `{}` |
| `policyTypes` | sequence of strings | `[Ingress, Egress]` |

**Kubernetes resource quantities** (`cpu`, `memory`) use a special string format - they are always strings in YAML, not numbers:

| Suffix | Meaning | Example |
|---|---|---|
| `m` | milli (1/1000) | `cpu: "100m"` = 0.1 CPU core |
| `Ki` | kibibyte (1024) | `memory: "64Ki"` |
| `Mi` | mebibyte (1024²) | `memory: "128Mi"` |
| `Gi` | gibibyte (1024³) | `memory: "2Gi"` |
| (none) | whole units | `cpu: "1"` = 1 full core |

Always quote quantity strings (`"100m"`, `"128Mi"`) to prevent YAML type coercion.

---

## 11. Using `kubectl explain` to Discover YAML Schema

`kubectl explain` is the primary tool for finding valid field names and types without memorising the full spec:

```bash
# Top-level fields of a resource
kubectl explain pod
kubectl explain deployment

# Drill into nested fields
kubectl explain pod.spec.containers
kubectl explain pod.spec.containers.env
kubectl explain networkpolicy.spec.ingress

# Print the full field tree recursively
kubectl explain --recursive deployment.spec
kubectl explain --recursive pod.spec.containers
```

Each field shows its **type** (`<[]Object>`, `<string>`, `<integer>`, `<map[string]string>`) and whether it is **required**. Use this during the exam whenever you are unsure of a field name or its structure.

```bash
# Example: discover NetworkPolicy ingress/from structure
kubectl explain networkpolicy.spec.ingress.from
# Shows: namespaceSelector, podSelector, ipBlock
```

> **Exam tip:** `kubectl explain pod.spec.containers --recursive | grep -i liveness` is faster than navigating docs to find probe field names.

---

## 12. Applying YAML from stdin and Heredocs

Kubernetes accepts YAML piped on stdin with `kubectl apply -f -`. Useful for one-liners, generated manifests, and scripts:

```bash
# Pipe a here-document directly - no file needed
kubectl apply -f - <<'EOF'
apiVersion: v1
kind: ConfigMap
metadata:
  name: my-config
data:
  key: value
EOF
```

```bash
# Pipe output of another command (dry-run → apply pattern)
kubectl create deployment web --image=nginx:1.27 --dry-run=client -o yaml \
  | kubectl apply -f -
```

```bash
# Apply multiple resources in one pipe
cat deployment.yaml service.yaml | kubectl apply -f -
```

> **Quoting the heredoc delimiter:** Use `<<'EOF'` (single-quoted) to prevent the shell from expanding `$VARIABLES` or `$(commands)` inside the YAML block. Use unquoted `<<EOF` only when you deliberately want shell expansion.

> **Exam tip:** `kubectl apply -f - <<'EOF' ... EOF` is the fastest way to create a resource from scratch - combine it with `kubectl explain` to discover field names and write the manifest inline without saving a file.

