# Dynamic API Configuration Solution

## Problem
The frontend application was hardcoded to use localhost URLs for API endpoints, which wouldn't work when deployed in different environments (e.g., different Minikube clusters, cloud deployments, exam environments).

## Solution
Implemented a dynamic API configuration system that automatically detects the correct API URLs based on the current environment.

## How It Works

### 1. Dynamic API Configuration (`/frontend/src/config/apiConfig.ts`)
```typescript
const getApiBaseUrl = () => {
  // Check if we're in development mode
  if (import.meta.env.DEV) {
    return 'http://localhost';
  }
  
  // In production, use the current hostname (works for any IP/domain)
  const currentHost = window.location.hostname;
  return `http://${currentHost}`;
};
```

This approach:
- **Development**: Uses `localhost` for local development
- **Production**: Uses `window.location.hostname` to automatically detect the current environment's IP/domain
- **Exam Environment**: Will automatically work with any Minikube IP or cloud deployment

### 2. NodePort Services
Backend services are exposed as NodePort with fixed port numbers:
- User Account Service: `30002`
- Query Service: `30004`
- Transaction Service: `30003`
- Frontend: `30247`

### 3. Environment-Agnostic URLs
The frontend automatically constructs API URLs like:
- `http://{current-ip}:30002` for User Account Service
- `http://{current-ip}:30004` for Query Service  
- `http://{current-ip}:30003` for Transaction Service

## Benefits

### ✅ Works in Any Environment
- **Minikube**: Automatically uses Minikube IP (e.g., `192.168.49.2`)
- **Cloud**: Automatically uses cloud provider's external IP
- **Exam**: Will work with any IP address without code changes

### ✅ No Hardcoded IPs
- No need to update configuration files for different environments
- No environment-specific builds required

### ✅ Development Friendly
- Still works with `localhost` during development
- Seamless transition between dev and production

### ✅ Future-Proof
- Works with domain names if DNS is configured
- Compatible with load balancers and ingress controllers

## Testing in Different Environments

To test this in a new environment:

1. **Start Minikube**: `minikube start`
2. **Apply manifests**: `kubectl apply -f K8s/`
3. **Get IP**: `minikube ip`
4. **Access**: `http://{minikube-ip}:30247`

The application will automatically configure itself to use the correct API endpoints!

## Migration Summary

### Before
- Hardcoded: `http://localhost:8002/api/Token`
- Required manual IP updates for each environment
- Would fail in exam/production environments

### After
- Dynamic: `http://{current-hostname}:30002/api/Token`
- Automatically adapts to any environment
- Zero configuration needed for deployment

This solution ensures the banking application will work seamlessly in any Kubernetes environment, including exam scenarios where the IP address is unknown beforehand.
