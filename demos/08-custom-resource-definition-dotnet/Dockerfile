# =============================================================================
# Multi-Stage Dockerfile for DatabaseBackup Controller — Educational Reference
# =============================================================================
#
# What: A multi-stage Docker build that compiles the .NET operator and
#   produces a minimal runtime image. Multi-stage builds keep the final
#   image small by excluding the SDK, source code, and build artifacts.
#
# How it works:
#
#   Stage 1: build                       Stage 2: runtime
#   ┌───────────────────────────┐        ┌────────────────────────────┐
#   │ sdk:10.0 (~900 MB)       │        │ runtime:10.0-alpine (~100MB)│
#   │                           │        │                            │
#   │ COPY *.csproj             │        │ COPY --from=build          │
#   │ dotnet restore            │───────>│   /app/publish             │
#   │                           │        │                            │
#   │ COPY . .                  │        │ USER 1001 (non-root)       │
#   │ dotnet publish            │        │                            │
#   │   -c Release              │        │ ENTRYPOINT [dotnet,        │
#   │   -o /app/publish         │        │ DatabaseBackupOperator.dll]│
#   └───────────────────────────┘        └────────────────────────────┘
#
# Security:
#   - Non-root user (USER 1001) — prevents privilege escalation
#   - Alpine base — smaller attack surface
#   - No shell in final image is optional but recommended for production
#
# Build & push:
#   docker build -t databasebackup-controller:2.0.0 .
#   docker tag databasebackup-controller:2.0.0 <registry>/databasebackup-controller:2.0.0
#   docker push <registry>/databasebackup-controller:2.0.0
#
# See also:
#   - DatabaseBackupOperator.csproj — project file restored in stage 1
#   - deployment.yaml — references the image built by this Dockerfile
# =============================================================================

# ---------------------------------------------------------------------------
# Stage 1: Build — restore dependencies and publish the app
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file first for layer caching — dependencies change rarely,
# so this layer is cached and only invalidated when the .csproj changes
COPY DatabaseBackupOperator.csproj .
RUN dotnet restore

# Copy remaining source and publish a self-contained release build
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---------------------------------------------------------------------------
# Stage 2: Runtime — minimal image with only the compiled app
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
WORKDIR /app

# Copy published output from the build stage
COPY --from=build /app/publish .

# Run as non-root for security — matches securityContext.runAsNonRoot in deployment.yaml
USER 1001

ENTRYPOINT ["dotnet", "DatabaseBackupOperator.dll"]
