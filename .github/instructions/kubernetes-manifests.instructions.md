---
applyTo: "**/*.{yaml,yml}"
---
# Kubernetes Manifest Conventions

This is an educational Kubernetes practices repository.
All YAML manifests must follow these conventions.

## Structure & Formatting

- Use 2-space indentation (no tabs)
- Order fields: `apiVersion`, `kind`, `metadata`, `spec`
- Separate multiple resources in one file with `---`
- Begin each file (or each resource block) with a comment explaining the resource's purpose and key concepts
- Add inline comments on non-obvious fields to explain *why*, not just *what*

## Naming

- Resource names: lowercase kebab-case (`myapp-pod`, `auth-deployment`, `db-service`)
- Filenames: numbered prefix matching folder order, kebab-case (`04-blue-green-deployment.yaml`)

## Labels & Selectors

- Always include an `app` label on all resources
- Use `type` label for role distinction (`frontend`, `backend`)
- Use `version` label for deployment strategy manifests (blue-green, canary)
- Ensure `selector.matchLabels` in Deployments/ReplicaSets matches `template.metadata.labels` exactly

## Images

- Use specific image tags (`nginx:1.21`), never `latest`

## Security Defaults

- Set `resources.requests` and `resources.limits` on containers in production-style manifests
- Prefer `runAsNonRoot: true` and `readOnlyRootFilesystem: true` in security contexts
- Avoid `privileged: true` unless explicitly demonstrating it

## Educational Comments

This repo is learning-focused. Every manifest should include:
1. A header comment block explaining the concept (what the resource does, key facts, common patterns)
2. Inline comments on fields that teach the reader
3. A footer section with commented `kubectl` commands showing how to create, inspect, and clean up the resource

Example footer pattern:
```yaml
# Apply this manifest:
# kubectl apply -f <filename>.yaml
#
# Inspect:
# kubectl get <resource> <name>
# kubectl describe <resource> <name>
#
# Clean up:
# kubectl delete -f <filename>.yaml
```

## Completing a File as an Educational Reference

When asked to complete, enrich, or turn a manifest into an educational reference, transform it into a comprehensive teaching document following this structure:

### 1. Title Banner
Start with a boxed comment banner naming the resource type:
```yaml
# =============================================================================
# Kubernetes <ResourceType> — Educational Reference
# =============================================================================
```

### 2. Concept Overview
Immediately after the banner, add a comment block that teaches the reader:
- **What** the resource is and what problem it solves
- **How it works** internally (e.g., kube-proxy, controllers, scheduling)
- **Key terminology** with definitions (list each term with `#   - term: explanation`)
- **Variants or modes** if the resource has multiple types (e.g., Service types, volume access modes, restart policies)
- Cross-reference related manifests in the repo where applicable (`# See 08-2-pod-config-map.yaml for ...`)

### 3. Multiple Variants with ASCII Diagrams
If the resource has variants (types, strategies, patterns), include a separate labeled manifest for each one. Before each variant add:
- A section-divider comment (`# ----...`)
- A numbered label (`# 1. ClusterIP Service (default)`)
- A short explanation of *when* to use this variant
- An ASCII traffic-flow diagram showing the request path:
```yaml
#   Client (inside cluster)
#       |
#       v
#   myapp-service:80  (ClusterIP)
#       |
#       v
#   Pod(s) with label app=myapp  :80 (targetPort)
```

### 4. Inline Field Comments
Every field in `spec` that a beginner might not understand gets an inline comment:
```yaml
  ports:
    - protocol: TCP
      port: 80             # Service port (what clients connect to)
      targetPort: 80       # Pod port (where traffic is forwarded)
      nodePort: 30088      # Node port (30000-32767). Omit to let K8s auto-assign.
```

### 5. Imperative Commands Section
After all resource definitions, add a full commented section of imperative equivalents grouped by operation:
- **Create** — `kubectl create` / `kubectl expose` commands with all relevant flags
- **Dry-run** — `kubectl ... --dry-run=client -o yaml` to generate YAML
- **Apply / Inspect** — `kubectl apply`, `kubectl get`, `kubectl describe`, `kubectl get -o yaml`, `kubectl get -o jsonpath='{...}'`
- **Watch / Debug** — `kubectl get --watch`, `kubectl logs`, `kubectl exec`
- **Testing** — connectivity tests using temp pods (`kubectl run --rm -it`), port-forward, DNS lookups
- **Delete / Clean up** — `kubectl delete -f`, `kubectl delete <resource> <name>`

Each command gets a one-line comment above it explaining what it does.

### 6. Completeness Checklist
When completing a file, ensure it covers:
- [ ] All major variants/types of the resource are demonstrated
- [ ] Every variant has an ASCII diagram showing data/traffic flow
- [ ] Every spec field has an inline comment
- [ ] Imperative create equivalents for each variant
- [ ] Inspect, debug, and testing commands specific to this resource type
- [ ] Cross-references to related files in the repo
- [ ] No placeholder or incomplete sections — the file should be self-contained

## Folder Organization

- `demos/` — Grouped by topic with numbered subfolders (`01-configuration/`, `02-multi-container/`, etc.)
- Files within each folder are numbered sequentially (`01-`, `02-`, ...)
- Related manifests share a number prefix (`08-1-config-map.yaml`, `08-2-pod-config-map.yaml`)
- Real-world application examples live in their own top-level folders (`kub-network/`, `kub-persistent-volume/`)

## API Versions

Use current stable apiVersions:

| Resource | apiVersion |
|----------|-----------|
| Pod, Service, ConfigMap, Secret, Namespace, ServiceAccount, PV, PVC | `v1` |
| Deployment, ReplicaSet, StatefulSet, DaemonSet | `apps/v1` |
| Job, CronJob | `batch/v1` |
| Ingress, NetworkPolicy | `networking.k8s.io/v1` |
| Role, RoleBinding, ClusterRole, ClusterRoleBinding | `rbac.authorization.k8s.io/v1` |
| HorizontalPodAutoscaler | `autoscaling/v2` |
| StorageClass | `storage.k8s.io/v1` |
| CustomResourceDefinition | `apiextensions.k8s.io/v1` |
