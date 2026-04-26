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

# 4. Verify rollout
kubectl rollout status deployment/fittrack-web
```

The app is reachable at **http://localhost:30080** once the pod is ready.

## Updating after a code change

```powershell
docker build -t fittrack-web:ci-latest .
# reload into cluster if needed (minikube/kind)
kubectl rollout restart deployment/fittrack-web
kubectl rollout status deployment/fittrack-web
```
