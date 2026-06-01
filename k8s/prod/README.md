# Payment Gateway Kubernetes Production Manifests

## Apply order

```bash
kubectl apply -f k8s/prod/namespace.yaml
kubectl apply -f k8s/prod/configmap.yaml
kubectl apply -f k8s/prod/secret.yaml
kubectl apply -f k8s/prod/service.yaml
kubectl apply -f k8s/prod/deployment.yaml
kubectl apply -f k8s/prod/hpa.yaml
kubectl apply -f k8s/prod/pdb.yaml
kubectl apply -f k8s/prod/ingress.yaml
kubectl apply -f k8s/prod/istio-gateway.yaml
kubectl apply -f k8s/prod/istio-virtualservice.yaml
```

## Replace before production

- `api.your-domain.com`
- `ghcr.io/YOUR_ORG/payment-gateway-api:latest`
- all values in `secret.yaml`
- project path in `.github/workflows/k8s-prod.yml` if your API project path differs
