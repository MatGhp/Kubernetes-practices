// =============================================================================
// Program Entry Point for DatabaseBackup Controller — Educational Reference
// =============================================================================
//
// What: The application entry point that wires up the KubeOps operator
//   framework using .NET Generic Host. This is the C# equivalent of the
//   Go main.go shown in the CRD controller examples.
//
// How it maps from Go:
//
//   Go                                      C#
//   ------------------------------------     ----------------------------------
//   ctrl.NewManager(ctrl.GetConfigOrDie(),   Host.CreateApplicationBuilder(args)
//     ctrl.Options{})                          .AddKubernetesOperator()
//
//   reconciler.SetupWithManager(mgr)         .RegisterComponents()
//                                            (auto-discovers DatabaseBackupController)
//
//   mgr.Start(ctrl.SetupSignalHandler())     host.RunAsync()
//                                            (handles SIGTERM/SIGINT gracefully)
//
// How it works inside the cluster:
//
//   Pod starts
//       |
//       v
//   Host.CreateApplicationBuilder()
//       |-- Reads KUBERNETES_SERVICE_HOST env var (set automatically by K8s)
//       |-- Uses in-cluster config (ServiceAccount token at
//       |   /var/run/secrets/kubernetes.io/serviceaccount/token)
//       |
//       v
//   AddKubernetesOperator()
//       |-- Creates KubernetesClient (authenticated via ServiceAccount)
//       |-- Sets up leader election (only one replica reconciles at a time)
//       |
//       v
//   RegisterComponents()
//       |-- Scans assembly for types with [KubernetesEntity] attribute
//       |   -> Finds V1DatabaseBackup
//       |-- Scans for IEntityController<T> implementations
//       |   -> Finds DatabaseBackupController
//       |-- Registers watch on DatabaseBackup resources via API server
//       |
//       v
//   host.RunAsync()
//       |-- Starts the reconciliation loop
//       |-- Listens for DatabaseBackup CR events
//       |-- Calls ReconcileAsync() / DeletedAsync() as events arrive
//       |-- Runs until SIGTERM (pod termination) or SIGINT (Ctrl+C)
//
// Prerequisites:
//   - DatabaseBackupOperator.csproj (NuGet packages)
//   - DatabaseBackupController.cs (entity + controller)
//   - When running in-cluster: a ServiceAccount with RBAC permissions
//     (see deployment.yaml)
//   - The CRD must be applied: kubectl apply -f 01-databasebackup-crd.yaml
//
// See also:
//   - DatabaseBackupController.cs — entity types and controller logic
//   - DatabaseBackupOperator.csproj — .NET project file
//   - Dockerfile — multi-stage Docker build
//   - deployment.yaml — K8s Deployment + RBAC
//   - 01-databasebackup-crd.yaml — CRD that registers the DatabaseBackup type
//   - 02-databasebackup-resource.yaml — sample DatabaseBackup instances
// =============================================================================

using KubeOps.Operator;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// --- Build the host ---

var builder = Host.CreateApplicationBuilder(args);

// Set log level — Trace shows all SDK internals, Information for production
builder.Logging.SetMinimumLevel(LogLevel.Information);

// --- Register the operator ---

builder.Services
    .AddKubernetesOperator()       // Wire up KubeOps (client, leader election, event loop)
    .RegisterComponents();         // Auto-discover V1DatabaseBackup + DatabaseBackupController

// --- Start ---

using var host = builder.Build();
await host.RunAsync();             // Blocks until SIGTERM/SIGINT — same as mgr.Start() in Go
