# FitTrack – Kubernetes deployment

## Prerequisites
- Local cluster running (Docker Desktop / minikube / kind)
- `kubectl` configured to target that cluster
- Image built and loaded into the cluster (see CI steps below)

## One-time secret setup
Create the Kubernetes secret that the Deployment reads from. Run this once per cluster (or whenever credentials change):

**PowerShell:**
```powershell
kubectl create secret generic fittrack-secrets `
  --from-literal=Postgres="Host=<pg-host>;Port=5432;Database=fittrack;Username=fittrack;Password=fittrack" `
  --from-literal=AzureAd__TenantId="<tenant-id>" `
  --from-literal=AzureAd__ClientId="<client-id>" `
  --from-literal=AzureAd__ClientSecret="<client-secret>" `
  --from-literal=AzureAd__Domain="<domain>.onmicrosoft.com"
```

**bash / WSL:**
```bash
kubectl create secret generic fittrack-secrets \
  --from-literal=Postgres="Host=<pg-host>;Port=5432;Database=fittrack;Username=fittrack;Password=fittrack" \
  --from-literal=AzureAd__TenantId="<tenant-id>" \
  --from-literal=AzureAd__ClientId="<client-id>" \
  --from-literal=AzureAd__ClientSecret="<client-secret>" \
  --from-literal=AzureAd__Domain="<domain>.onmicrosoft.com"
```

See `secret.template.yaml` for the manifest equivalent (do **not** commit a populated copy).

## Updating secrets
To replace all secret values (e.g. after rotating the client secret), delete and recreate the secret, then restart the pod to pick up the new values:

**PowerShell:**
```powershell
kubectl delete secret fittrack-secrets

kubectl create secret generic fittrack-secrets `
  --from-literal=Postgres="Host=<pg-host>;Port=5432;Database=fittrack;Username=fittrack;Password=fittrack" `
  --from-literal=AzureAd__TenantId="<tenant-id>" `
  --from-literal=AzureAd__ClientId="<client-id>" `
  --from-literal=AzureAd__ClientSecret="<client-secret>" `
  --from-literal=AzureAd__Domain="<domain>.onmicrosoft.com"

kubectl rollout restart deployment/fittrack-web
kubectl rollout status deployment/fittrack-web
```

**bash / WSL:**
```bash
kubectl delete secret fittrack-secrets

kubectl create secret generic fittrack-secrets \
  --from-literal=Postgres="Host=<pg-host>;Port=5432;Database=fittrack;Username=fittrack;Password=fittrack" \
  --from-literal=AzureAd__TenantId="<tenant-id>" \
  --from-literal=AzureAd__ClientId="<client-id>" \
  --from-literal=AzureAd__ClientSecret="<client-secret>" \
  --from-literal=AzureAd__Domain="<domain>.onmicrosoft.com"

kubectl rollout restart deployment/fittrack-web
kubectl rollout status deployment/fittrack-web
```

To update only a single key without touching the others, use `kubectl patch`:

```powershell
kubectl patch secret fittrack-secrets `
  --type=merge `
  -p '{\"stringData\":{\"AzureAd__ClientSecret\":\"<new-secret>\"}}'
kubectl rollout restart deployment/fittrack-web
```

## CI deploy steps

```powershell
# 1. Build the image
docker build -t fittrack-web:ci-latest .

# 2. Load into the local cluster (pick the one that matches your setup)
#    Docker Desktop:  image is already available — skip this step
#    minikube:        minikube image load fittrack-web:ci-latest
#    kind:            kind load docker-image fittrack-web:ci-latest

# 3. Apply manifests
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
# Note: backup-pvc.yaml is only needed if you switch to the PVC volume approach

# 4. Verify rollout
kubectl rollout status deployment/fittrack-web
```

The app is reachable at **http://localhost:30080** once the pod is ready.

## Backup storage

### Local cluster (Docker Desktop) — hostPath volume
`deployment.yaml` uses a `hostPath` volume so backups are written directly to your Windows machine at:

```
C:\FitTrack\backups\
```

Docker Desktop maps the special Linux path `/run/desktop/mnt/host/c/...` to the corresponding `C:\...` Windows path. The folder is created automatically the first time the pod starts. You can browse backup zips in Explorer like any other folder — no `kubectl` commands needed.

> **Note:** If you are using minikube or kind instead of Docker Desktop, the `hostPath` points to a folder inside the VM, not your Windows filesystem. In that case switch back to the PVC approach — uncomment the `persistentVolumeClaim` block in `deployment.yaml` and apply `backup-pvc.yaml` first.

### Production / real cluster — PersistentVolumeClaim
For a non-local cluster, replace the `hostPath` volume in `deployment.yaml` with:

```yaml
      volumes:
        - name: backups
          persistentVolumeClaim:
            claimName: fittrack-backups-pvc
```

And apply the PVC once:
```powershell
kubectl apply -f k8s/backup-pvc.yaml
```

To copy backups down from a PVC-backed pod:
```powershell
# List backups
kubectl exec deployment/fittrack-web -- ls /data/backups

# Copy all backups to local machine
kubectl cp fittrack-web/<pod-name>:/data/backups ./local-backups
```

## Updating after a code change

```powershell
docker build -t fittrack-web:ci-latest .
# reload into cluster if needed (minikube/kind)
kubectl rollout restart deployment/fittrack-web
kubectl rollout status deployment/fittrack-web
```
