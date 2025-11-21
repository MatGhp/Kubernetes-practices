# Kubernetes Practices & Learning Repository

A comprehensive collection of Kubernetes configurations, examples, and practical implementations for learning and mastering Kubernetes concepts. This repository serves as a hands-on guide covering fundamental to advanced Kubernetes topics.

## 📋 Table of Contents

- [Overview](#overview)
- [Repository Structure](#repository-structure)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Learning Path](#learning-path)
- [Demos Overview](#demos-overview)
- [Projects](#projects)
- [Usage Examples](#usage-examples)
- [Contributing](#contributing)
- [License](#license)

## 🎯 Overview

This repository contains practical Kubernetes examples and demonstrations organized by topic. Each section includes YAML configuration files with detailed comments and kubectl commands to help you understand and practice Kubernetes concepts.

**What you'll learn:**
- Core Kubernetes objects (Pods, Deployments, Services, etc.)
- Configuration management (ConfigMaps, Secrets)
- Networking and service discovery
- State persistence and storage
- Security best practices
- Advanced patterns (CRDs, Kustomize)
- Multi-container design patterns
- Observability and monitoring

## 📁 Repository Structure

```
.
├── demos/                          # Organized learning demos by topic
│   ├── 01-configuration/           # Core K8s objects and configuration
│   ├── 02-multi-container/         # Multi-container pod patterns
│   ├── 03-observability/           # Health checks and probes
│   ├── 04-pod-design/              # Labels, annotations, deployments
│   ├── 05-services-and-networking/ # Services, Ingress, Network Policies
│   ├── 06-state-persistence/       # Volumes, PV, PVC, StatefulSets
│   ├── 07-security/                # RBAC, Security contexts
│   ├── 08-custom-resource-definition/ # Custom Resources (CRDs)
│   └── 09-kustomize/               # Kustomize configurations
├── kub-network/                    # Multi-service networking demo
│   ├── auth-api/                   # Authentication service
│   ├── users-api/                  # Users management service
│   └── tasks-api/                  # Tasks management service
└── kub-persistent-volume/          # Persistent volume demo application
```

## 🔧 Prerequisites

Before getting started, ensure you have the following installed:

- **Docker** (v20.10 or later) - [Install Docker](https://docs.docker.com/get-docker/)
- **Kubernetes Cluster** - Choose one:
  - [Minikube](https://minikube.sigs.k8s.io/docs/start/) (Recommended for local development)
  - [Kind](https://kind.sigs.k8s.io/) (Kubernetes in Docker)
  - [Docker Desktop](https://www.docker.com/products/docker-desktop) (includes Kubernetes)
  - Cloud provider (GKE, EKS, AKS)
- **kubectl** (v1.20 or later) - [Install kubectl](https://kubernetes.io/docs/tasks/tools/)
- **Docker Compose** (for running multi-service projects locally)

### Verify Installation

```bash
# Check Docker
docker --version

# Check Kubernetes cluster
kubectl cluster-info

# Check kubectl
kubectl version --client
```

## 🚀 Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/MatGhp/Kubernetes-practices.git
cd Kubernetes-practices
```

> **Note:** Replace `MatGhp` with your GitHub username if you've forked this repository.

### 2. Start Your Kubernetes Cluster

**Using Minikube:**
```bash
minikube start
```

**Using Docker Desktop:**
Enable Kubernetes in Docker Desktop settings.

### 3. Verify Cluster Access

```bash
kubectl get nodes
kubectl get namespaces
```

### 4. Try Your First Demo

```bash
cd demos/01-configuration
kubectl apply -f 01-pod-definition.yaml
kubectl get pods
kubectl describe pod myapp-pod
```

## 📚 Learning Path

Follow this recommended path for structured learning:

### Beginner Level
1. **01-configuration/** - Start here to learn core Kubernetes objects
   - Pods, ReplicaSets, Deployments
   - Services and basic networking
   - ConfigMaps and Secrets
   - Namespaces and Resource Quotas

2. **03-observability/** - Learn about application health
   - Liveness probes
   - Readiness probes

### Intermediate Level
3. **04-pod-design/** - Master pod lifecycle and deployment strategies
   - Labels and selectors
   - Rolling updates and rollbacks
   - Blue-Green and Canary deployments
   - Jobs and CronJobs

4. **05-services-and-networking/** - Deep dive into networking
   - Service types (ClusterIP, NodePort, LoadBalancer)
   - Ingress controllers
   - Network policies

5. **06-state-persistence/** - Handle stateful applications
   - Volumes and Volume Mounts
   - Persistent Volumes (PV)
   - Persistent Volume Claims (PVC)
   - Storage Classes
   - StatefulSets

### Advanced Level
6. **02-multi-container/** - Multi-container design patterns
   - Sidecar pattern
   - Ambassador pattern
   - Adapter pattern

7. **07-security/** - Secure your clusters
   - RBAC (Role-Based Access Control)
   - Security contexts
   - Pod security policies
   - Admission webhooks

8. **08-custom-resource-definition/** - Extend Kubernetes
   - Custom Resource Definitions (CRDs)
   - Custom controllers

9. **09-kustomize/** - Configuration management
   - Kustomize basics
   - Multi-environment configurations

## 🎪 Demos Overview

### 01-configuration
Core Kubernetes configurations including:
- Pod definitions
- ReplicationControllers and ReplicaSets
- Deployments
- Services
- ConfigMaps and Secrets
- Namespaces and Resource Quotas
- Security contexts and Service Accounts
- Taints, Tolerations, and Node Affinity

**Example commands:**
```bash
cd demos/01-configuration
kubectl apply -f 04-deployment-definition.yaml
kubectl get deployments
kubectl scale deployment myapp-deployment --replicas=5
```

### 02-multi-container
Multi-container pod patterns demonstrating:
- Co-located containers
- Sidecar pattern for logging/monitoring

### 03-observability
Health check configurations:
- Liveness probes (restart unhealthy containers)
- Readiness probes (control traffic routing)

### 04-pod-design
Pod management and deployment strategies:
- Labels and annotations
- Rolling updates and rollbacks
- Blue-Green deployments
- Canary deployments
- Jobs and CronJobs

### 05-services-and-networking
Networking concepts:
- Service definitions (ClusterIP, NodePort, LoadBalancer)
- Ingress configuration
- Network policies for pod-to-pod communication

### 06-state-persistence
Stateful application management:
- Volume mounts
- Persistent Volumes and Claims
- Storage Classes
- StatefulSets
- Headless Services

### 07-security
Security best practices:
- Kubernetes configuration files
- RBAC roles and bindings
- ClusterRoles and ClusterRoleBindings
- Validating webhooks

### 08-custom-resource-definition
Extending Kubernetes:
- Custom Resource Definition example (FlightTicket)
- Custom resource instances
- Basic controller implementation

### 09-kustomize
Configuration management with Kustomize:
- Base and overlay structure
- Multi-folder organization
- Environment-specific configurations

## 🏗️ Projects

### kub-network
A complete microservices demonstration with three interconnected services:

**Services:**
- **auth-api**: Authentication service
- **users-api**: User management (depends on auth-api)
- **tasks-api**: Task management (depends on auth-api)

**Running with Docker Compose:**
```bash
cd kub-network
docker-compose up -d --build
```

**Testing:**
```bash
# Test users service
curl -X POST http://localhost:8080/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@test.com","password":"testers"}'

# Test tasks service
curl -X POST http://localhost:8000/tasks \
  -H "Content-Type: application/json" \
  -d '{"text":"a task","title":"Do this, too"}'

curl http://localhost:8000/tasks
```

**Deploying to Kubernetes:**
```bash
cd kub-network/kubernetes
kubectl apply -f auth-deployment.yaml -f auth-service.yaml
kubectl apply -f users-deployment.yaml -f users-service.yaml
kubectl apply -f tasks-deployment.yaml -f tasks-service.yaml

# Access services (Minikube)
minikube service users-service
minikube service tasks-service
```

### kub-persistent-volume
A demonstration of persistent storage in Kubernetes with a simple Node.js application.

**Features:**
- Persistent Volume (PV) and Persistent Volume Claim (PVC)
- Environment variables configuration
- Volume mounting for data persistence

**Building and Deploying:**
```bash
cd kub-persistent-volume

# Build Docker image (replace <your-dockerhub-username> and tag as needed)
docker build -t <your-dockerhub-username>/kub-data-demo:latest .
docker push <your-dockerhub-username>/kub-data-demo:latest

# Note: Update the image reference in deployment.yaml to match your image name and tag

# Deploy to Kubernetes
kubectl apply -f environment.yaml
kubectl apply -f host-pv.yaml
kubectl apply -f host-pvc.yaml
kubectl apply -f deployment.yaml
kubectl apply -f service.yaml

# Check deployment
kubectl get pods
kubectl get pv
kubectl get pvc
kubectl get services
```

## 💡 Usage Examples

### Creating a Simple Pod
```bash
kubectl run nginx --image=nginx:latest
kubectl get pods
kubectl describe pod nginx
kubectl logs nginx
kubectl delete pod nginx
```

### Working with Deployments
```bash
# Create deployment
kubectl create deployment nginx --image=nginx:latest --replicas=3

# Scale deployment
kubectl scale deployment nginx --replicas=5

# Update image
kubectl set image deployment/nginx nginx=nginx:1.21

# Check rollout status
kubectl rollout status deployment/nginx

# View rollout history
kubectl rollout history deployment/nginx

# Rollback
kubectl rollout undo deployment/nginx
```

### Working with Services
```bash
# Expose deployment
kubectl expose deployment nginx --port=80 --type=NodePort

# Get service details
kubectl get services
kubectl describe service nginx

# Access service (Minikube)
minikube service nginx
```

### Using ConfigMaps and Secrets
```bash
# Create ConfigMap
kubectl create configmap app-config --from-literal=APP_ENV=production

# Create Secret
kubectl create secret generic app-secret --from-literal=API_KEY=secret123

# Apply configuration with ConfigMap/Secret
kubectl apply -f demos/01-configuration/08-pod-config-map-definition.yaml
```

### Debugging
```bash
# Get pod logs
kubectl logs <pod-name>

# Follow logs
kubectl logs -f <pod-name>

# Execute command in pod
kubectl exec -it <pod-name> -- /bin/bash

# Port forward
kubectl port-forward <pod-name> 8080:80

# Get events
kubectl get events --sort-by=.metadata.creationTimestamp
```

## 🤝 Contributing

Contributions are welcome! If you have improvements, additional examples, or bug fixes:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Kubernetes documentation and community
- Various tutorials and learning resources that inspired these examples
- Contributors and maintainers of this repository

## 📮 Support

If you find this repository helpful, please consider giving it a ⭐️!

For questions or issues, please open an issue in the GitHub repository.

---

**Happy Learning! 🚀**
