// =============================================================================
// Kubernetes Custom Controller for DatabaseBackup in C# — Educational Reference
// =============================================================================
//
// What: A C# Kubernetes controller built with the KubeOps SDK
//   (https://github.com/dotnet/dotnet-operator-sdk). It watches for DatabaseBackup
//   Custom Resources and reconciles them — validating backup configurations,
//   simulating backup execution, and updating status with real-world error
//   handling and idempotency.
//
// Why C#: Teams with existing .NET investment can build Kubernetes operators
//   without switching to Go. KubeOps provides the same abstractions as Go's
//   controller-runtime: entity types, reconcilers, finalizers, RBAC generation,
//   and leader election — all wired through the .NET Generic Host.
//
// How it maps from Go:
//
//   Go (controller-runtime)               C# (KubeOps)
//   ─────────────────────────────────────  ────────────────────────────────────
//   DatabaseBackup struct                  V1DatabaseBackup class (partial)
//     metav1.TypeMeta / ObjectMeta           inherits CustomKubernetesEntity
//     DatabaseBackupSpec struct              DatabaseBackupSpec class
//     DatabaseBackupStatus struct            DatabaseBackupStatus class
//   DatabaseBackupReconciler struct        DatabaseBackupController class
//     Reconcile(ctx, req)                    ReconcileAsync(entity, ct)
//     client.Client (embedded)               IKubernetesClient (injected)
//   ctrl.NewManager / SetupWithManager     Host.CreateApplicationBuilder +
//                                            AddKubernetesOperator()
//   r.Status().Update(ctx, &backup)        _client.UpdateStatus(entity)
//   schema.GroupVersion{...}               [KubernetesEntity(...)] attribute
//   // +kubebuilder:rbac                   [EntityRbac(...)] attribute
//
// Reconcile flow:
//
//   API Server                     Controller (.NET)
//       |                              |
//       |--- Watch DatabaseBackups --->|
//       |                              |
//   CR created (kubectl apply)         |
//       |--- Event: CR added --------->|
//       |                              |-- ReconcileAsync()
//       |                              |   1. Validate spec (databaseName,
//       |                              |      schedule, retentionDays,
//       |                              |      storageLocation)
//       |                              |   2. Skip if Completed/Running
//       |                              |      (idempotent)
//       |                              |   3. Set status → Running
//       |                              |   4. Simulate backup execution
//       |                              |   5. Set status → Completed with
//       |                              |      backupSizeBytes, timestamps
//       |                              |   6. Update status subresource
//       |<-- Status update ------------|
//
// IMPORTANT — partial keyword:
//   The entity class MUST be declared as `partial` so the KubeOps.Generator
//   source generator can auto-create a constructor that sets ApiVersion and
//   Kind fields. Without `partial`, the entity won't serialize correctly and
//   API server requests will fail with 422 Unprocessable Entity.
//
// Prerequisites:
//   - .NET 10.0+ SDK
//   - KubeOps.Operator NuGet package: dotnet add package KubeOps.Operator
//   - The CRD must be applied: kubectl apply -f 01-databasebackup-crd.yaml
//
// See also:
//   - 01-databasebackup-crd.yaml — defines the DatabaseBackup schema
//   - 02-databasebackup-resource.yaml — example DatabaseBackup instances
//   - Program.cs — entry point that starts the operator
//   - DatabaseBackupOperator.csproj — project file with NuGet dependencies
//   - deployment.yaml — K8s Deployment + RBAC manifests
// =============================================================================

using System.Text.Json.Serialization;
using k8s.Models;
using KubeOps.Abstractions.Entities;
using KubeOps.Abstractions.Entities.Attributes;
using KubeOps.Abstractions.Rbac;
using KubeOps.Abstractions.Reconciliation;
using KubeOps.Abstractions.Reconciliation.Controller;
using KubeOps.KubernetesClient;
using Microsoft.Extensions.Logging;

// =============================================================================
// Custom Resource Types
// =============================================================================

/// <summary>
/// DatabaseBackup defines the structure of the DatabaseBackup custom resource.
///
/// IMPORTANT: The class MUST be `partial` so that KubeOps.Generator can
/// auto-generate a parameterless constructor that sets:
///   ApiVersion = "backup.example.com/v1";
///   Kind = "DatabaseBackup";
/// Without this, the Kubernetes client cannot serialize the entity correctly.
/// </summary>
[KubernetesEntity(                               // Registers this type with the API group/version
    Group = "backup.example.com",                //   API group — appears in apiVersion: backup.example.com/v1
    ApiVersion = "v1",                           //   API version
    Kind = "DatabaseBackup",                     //   Resource kind (PascalCase)
    PluralName = "databasebackups")]             //   Plural name for REST endpoints
[KubernetesEntityShortNames("dbb")]              // kubectl get dbb
public partial class V1DatabaseBackup            // ← partial is REQUIRED for KubeOps.Generator
    : CustomKubernetesEntity<                    // Base class provides metadata + spec + status
        V1DatabaseBackup.DatabaseBackupSpec,
        V1DatabaseBackup.DatabaseBackupStatus>
{
    /// <summary>
    /// DatabaseBackupSpec defines the desired state — what the user specifies
    /// when creating a DatabaseBackup CR.
    /// </summary>
    public class DatabaseBackupSpec
    {
        [JsonPropertyName("databaseName")]           // JSON field name matches CRD schema
        public string DatabaseName { get; set; } = "";   // Name of the database to back up (required)

        [JsonPropertyName("schedule")]
        public string Schedule { get; set; } = "";   // Cron expression, e.g. "0 2 * * *" (daily at 2 AM)

        [JsonPropertyName("retentionDays")]
        public int RetentionDays { get; set; }       // How many days to keep backups (1–365)

        [JsonPropertyName("storageLocation")]
        public string StorageLocation { get; set; } = "";  // Target path or bucket, e.g. "s3://backups/mydb"
    }

    /// <summary>
    /// DatabaseBackupStatus defines the observed state — what the controller reports
    /// back after reconciliation. Updated via the /status subresource.
    /// </summary>
    public class DatabaseBackupStatus
    {
        [JsonPropertyName("phase")]
        public string Phase { get; set; } = "";              // Pending | Running | Completed | Failed

        [JsonPropertyName("lastBackupTime")]
        public string LastBackupTime { get; set; } = "";     // When the last backup completed (ISO 8601)

        [JsonPropertyName("nextBackupTime")]
        public string NextBackupTime { get; set; } = "";     // When the next backup is scheduled (ISO 8601)

        [JsonPropertyName("backupSizeBytes")]
        public long BackupSizeBytes { get; set; }            // Size of last backup in bytes

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";            // Human-readable status message for observability

        [JsonPropertyName("lastUpdated")]
        public string LastUpdated { get; set; } = "";        // ISO 8601 timestamp of last status change
    }
}

// =============================================================================
// Controller (Reconciler)
// =============================================================================

/// <summary>
/// DatabaseBackupController reconciles DatabaseBackup objects.
///
/// Real-world features:
///   1. Input validation — rejects invalid spec with status.phase = "Failed"
///   2. Idempotency — skips reconciliation if already "Completed" or "Running"
///   3. Status timestamps — records when each status change occurred
///   4. Error handling — catches and logs failures during status updates
///   5. Structured logging — uses log templates for observability
///
/// NOTE: This educational sample simulates the backup operation. In a
/// production operator, ReconcileAsync would call an external backup tool
/// (pg_dump, mysqldump, etc.) or create a Kubernetes Job to perform the
/// actual backup. The simulation generates a random backup size and
/// calculates the next backup time based on the schedule.
/// </summary>
[EntityRbac(typeof(V1DatabaseBackup), Verbs = RbacVerb.All)]
public class DatabaseBackupController : IEntityController<V1DatabaseBackup>
{
    private readonly IKubernetesClient _client;
    private readonly ILogger<DatabaseBackupController> _logger;

    public DatabaseBackupController(
        IKubernetesClient client,
        ILogger<DatabaseBackupController> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// ReconcileAsync is called whenever a DatabaseBackup CR is created or updated.
    ///
    /// Flow:
    ///   1. Validate spec fields (databaseName, schedule, retentionDays, storageLocation)
    ///   2. If invalid → set status.phase = "Failed" with message, return
    ///   3. If already Completed or Running → skip (idempotent), return
    ///   4. Set status.phase = "Running"
    ///   5. Simulate backup execution (generate size, calculate next time)
    ///   6. Set status.phase = "Completed" with backupSizeBytes, timestamps
    ///   7. Update status subresource via API server
    /// </summary>
    public async Task<ReconciliationResult<V1DatabaseBackup>> ReconcileAsync(
        V1DatabaseBackup entity,
        CancellationToken cancellationToken)
    {
        var name = entity.Metadata.Name;
        var ns = entity.Metadata.NamespaceProperty ?? "default";

        _logger.LogInformation(
            "Reconciling DatabaseBackup {Namespace}/{Name}: database={Database}, schedule={Schedule}, retention={Retention}d, storage={Storage}",
            ns, name, entity.Spec.DatabaseName, entity.Spec.Schedule,
            entity.Spec.RetentionDays, entity.Spec.StorageLocation);

        // -----------------------------------------------------------------
        // 1. Validate spec — reject invalid input with a Failed status
        // -----------------------------------------------------------------
        var validationError = ValidateSpec(entity.Spec);
        if (validationError is not null)
        {
            _logger.LogWarning(
                "Validation failed for DatabaseBackup {Namespace}/{Name}: {Error}",
                ns, name, validationError);

            entity.Status.Phase = "Failed";
            entity.Status.Message = validationError;
            entity.Status.LastUpdated = DateTime.UtcNow.ToString("o");

            entity = await _client.UpdateStatus(entity);
            return ReconciliationResult<V1DatabaseBackup>.Success(entity);
        }

        // -----------------------------------------------------------------
        // 2. Idempotency — skip if already completed or running
        // -----------------------------------------------------------------
        if (entity.Status.Phase is "Completed" or "Running")
        {
            _logger.LogDebug(
                "DatabaseBackup {Namespace}/{Name} already {Phase}, skipping",
                ns, name, entity.Status.Phase);
            return ReconciliationResult<V1DatabaseBackup>.Success(entity);
        }

        // -----------------------------------------------------------------
        // 3. Set status to Running — indicates backup is in progress
        // -----------------------------------------------------------------
        entity.Status.Phase = "Running";
        entity.Status.Message = $"Starting backup of {entity.Spec.DatabaseName} to {entity.Spec.StorageLocation}";
        entity.Status.LastUpdated = DateTime.UtcNow.ToString("o");

        entity = await _client.UpdateStatus(entity);

        _logger.LogInformation(
            "DatabaseBackup {Namespace}/{Name}: backup started for database {Database}",
            ns, name, entity.Spec.DatabaseName);

        // -----------------------------------------------------------------
        // 4. Simulate backup execution
        //    In production, this would:
        //      - Spawn a Job running pg_dump / mysqldump / az storage blob
        //      - Wait for Job completion
        //      - Read the actual backup size from the storage backend
        //    For this educational sample, we generate a realistic random size.
        // -----------------------------------------------------------------
        var now = DateTime.UtcNow;
        var random = new Random();
        var backupSizeBytes = random.NextInt64(
            50 * 1024 * 1024,      // Minimum 50 MB
            2L * 1024 * 1024 * 1024 // Maximum 2 GB
        );

        // Calculate next backup time (24 hours from now as a simple default).
        // A production operator would parse the cron expression from spec.schedule
        // using a library like Cronos to compute the exact next run time.
        var nextBackupTime = now.AddHours(24);

        // -----------------------------------------------------------------
        // 5. Set status to Completed with backup details
        // -----------------------------------------------------------------
        entity.Status.Phase = "Completed";
        entity.Status.LastBackupTime = now.ToString("o");
        entity.Status.NextBackupTime = nextBackupTime.ToString("o");
        entity.Status.BackupSizeBytes = backupSizeBytes;
        entity.Status.Message = $"Backup completed: {entity.Spec.DatabaseName} "
            + $"({backupSizeBytes / (1024 * 1024)} MB) stored at {entity.Spec.StorageLocation}";
        entity.Status.LastUpdated = DateTime.UtcNow.ToString("o");

        // IMPORTANT: Always use the returned entity from UpdateStatus.
        // The API server increments resourceVersion on every write.
        // Ignoring the return value causes HTTP 409 Conflict on the next update.
        try
        {
            entity = await _client.UpdateStatus(entity);

            _logger.LogInformation(
                "DatabaseBackup completed: {Namespace}/{Name}, database={Database}, size={SizeMB} MB, nextBackup={NextBackup}",
                ns, name, entity.Spec.DatabaseName,
                entity.Status.BackupSizeBytes / (1024 * 1024),
                entity.Status.NextBackupTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to update status for DatabaseBackup {Namespace}/{Name}",
                ns, name);
            throw; // Let KubeOps retry via the reconciliation loop
        }

        return ReconciliationResult<V1DatabaseBackup>.Success(entity);
    }

    /// <summary>
    /// DeletedAsync is called when a DatabaseBackup CR is deleted.
    /// Used for cleanup logging. In a real-world operator, this could:
    ///   - Delete the actual backup files from storage
    ///   - Cancel any in-progress backup Jobs
    ///   - Notify downstream monitoring systems
    /// </summary>
    public Task<ReconciliationResult<V1DatabaseBackup>> DeletedAsync(
        V1DatabaseBackup entity,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "DatabaseBackup deleted: {Namespace}/{Name}, database={Database}, lastBackup={LastBackup}",
            entity.Metadata.NamespaceProperty ?? "default",
            entity.Metadata.Name,
            entity.Spec.DatabaseName,
            entity.Status.LastBackupTime);

        return Task.FromResult(
            ReconciliationResult<V1DatabaseBackup>.Success(entity));
    }

    // =====================================================================
    // Private helpers
    // =====================================================================

    /// <summary>
    /// Validates the DatabaseBackup spec fields. Returns null if valid,
    /// or an error message string if validation fails.
    /// This mirrors what the CRD's openAPIV3Schema enforces at admission time,
    /// but provides defense-in-depth within the controller itself.
    /// </summary>
    private static string? ValidateSpec(V1DatabaseBackup.DatabaseBackupSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.DatabaseName))
            return "spec.databaseName is required and cannot be empty";

        if (string.IsNullOrWhiteSpace(spec.Schedule))
            return "spec.schedule is required and cannot be empty";

        if (spec.RetentionDays < 1 || spec.RetentionDays > 365)
            return $"spec.retentionDays must be between 1 and 365, got {spec.RetentionDays}";

        if (string.IsNullOrWhiteSpace(spec.StorageLocation))
            return "spec.storageLocation is required and cannot be empty";

        // NOTE: A production operator would also validate:
        //   - spec.schedule is a valid cron expression (using Cronos library)
        //   - spec.storageLocation is a reachable storage endpoint
        //   - spec.databaseName matches an existing database

        return null; // Valid
    }
}

// =============================================================================
// Go-to-C# Concept Mapping Summary
// =============================================================================
//
//   Concept                 Go                              C# (KubeOps)
//   ─────────────────────   ─────────────────────────────   ──────────────────────────────
//   CR type definition      struct + metav1 embeds          partial class + CustomKubernetesEntity
//   API group/version       schema.GroupVersion var          [KubernetesEntity] attribute
//   Controller              struct embedding client.Client   class implementing IEntityController
//   Reconcile function      Reconcile(ctx, req)             ReconcileAsync(entity, ct)
//   Deletion handling       IgnoreNotFound in Reconcile     Separate DeletedAsync method
//   Status update           r.Status().Update(ctx, &obj)    _client.UpdateStatus(entity)
//   Manager setup           ctrl.NewManager(opts)           AddKubernetesOperator()
//   Register controller     SetupWithManager(mgr)           RegisterComponents() (via Generator)
//   RBAC generation         // +kubebuilder:rbac comment    [EntityRbac] attribute
//   DeepCopy                Must implement DeepCopyObject   Not needed (SDK handles it)
//   Dependency injection    Embed fields in struct          Constructor injection (DI)
//   Entry point             func main() { mgr.Start() }    Host.CreateApplicationBuilder
