# AKS Cluster Patching Runbook

## Overview
AKS patching has two levels:
1. **Node image upgrade** — updates the OS image on existing nodes (minimal disruption)
2. **Kubernetes version upgrade** — updates the control plane and node version (requires planned downtime)

## Prerequisites
- [ ] Wiz scan reviewed for container/node vulnerabilities
- [ ] Staging cluster upgraded first
- [ ] Application compatibility verified
- [ ] Maintenance window approved (for k8s version upgrades)

## Node Image Upgrade (Low Risk)
```powershell
# Check current node image version
az aks nodepool list --resource-group <RG> --cluster-name <CLUSTER> --query "[].{name:name, imageType:nodeImageVersion}"

# Upgrade node image (rolling, no control plane restart)
az aks nodepool upgrade --resource-group <RG> --cluster-name <CLUSTER> `
    --name <NODEPOOL> --node-image-only --no-wait
```

## Kubernetes Version Upgrade (Higher Risk)
```powershell
# Check available versions
az aks get-versions --location <REGION> --query "orchestrators[].orchestratorVersion"

# Upgrade control plane first
az aks upgrade --resource-group <RG> --cluster-name <CLUSTER> `
    --kubernetes-version <TARGET_VERSION> --control-plane-only

# Upgrade node pools
az aks nodepool upgrade --resource-group <RG> --cluster-name <CLUSTER> `
    --name <NODEPOOL> --kubernetes-version <TARGET_VERSION>
```

## Surge Upgrade (Zero Downtime)
```powershell
az aks nodepool upgrade --resource-group <RG> --cluster-name <CLUSTER> `
    --name <NODEPOOL> --kubernetes-version <TARGET_VERSION> `
    --max-surge 33% --no-wait
```

## Rollback
- AKS does not support downgrading. Rollback means creating a new cluster.
- Always test on staging first.
- Ensure PDBs (Pod Disruption Budgets) are configured.

## Post-Upgrade
- [ ] Verify node pool status: `az aks nodepool list`
- [ ] Check pod health: `kubectl get pods --all-namespaces`
- [ ] Wiz re-scan
- [ ] Update compliance log
