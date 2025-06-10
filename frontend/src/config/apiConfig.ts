// API Configuration utility that works in any environment
// This dynamically determines the correct API URLs based on the current environment

const getApiConfig = () => {
  // Use environment variables if available (for Kubernetes/Docker deployments)
  if (import.meta.env.VITE_USER_SERVICE_URL) {
    return {
      USER_SERVICE: import.meta.env.VITE_USER_SERVICE_URL,
      TRANSACTION_SERVICE: import.meta.env.VITE_TRANSACTION_SERVICE_URL,
      QUERY_SERVICE: import.meta.env.VITE_QUERY_SERVICE_URL,
    };
  }
  
  // Fallback for development mode
  if (import.meta.env.DEV) {
    return {
      USER_SERVICE: 'http://localhost:8002',
      TRANSACTION_SERVICE: 'http://localhost:8003',
      QUERY_SERVICE: 'http://localhost:8004',
    };
  }
  
  // In production without env vars, use the current hostname (works for any IP/domain)
  const currentHost = window.location.hostname;
  const baseUrl = `http://${currentHost}`;
  return {
    USER_SERVICE: `${baseUrl}:8002`,
    TRANSACTION_SERVICE: `${baseUrl}:8003`,
    QUERY_SERVICE: `${baseUrl}:8004`,
  };
};

// API endpoints configuration
export const API_CONFIG = {
  ...getApiConfig(),
  
  // API paths
  AUTH_API: '/api/Token',
  ACCOUNT_API: '/api/Account',
  TRANSACTION_API: '/api/Transaction',
  GRAPHQL_API: '/graphql'
};

// Full API URLs
export const API_URLS = {
  AUTH: `${API_CONFIG.USER_SERVICE}${API_CONFIG.AUTH_API}`,
  ACCOUNT: `${API_CONFIG.USER_SERVICE}${API_CONFIG.ACCOUNT_API}`,
  TRANSACTION: `${API_CONFIG.TRANSACTION_SERVICE}${API_CONFIG.TRANSACTION_API}`,
  GRAPHQL: `${API_CONFIG.QUERY_SERVICE}${API_CONFIG.GRAPHQL_API}`,
  USER: `${API_CONFIG.USER_SERVICE}${API_CONFIG.AUTH_API}` // Uses same service as auth
};

console.log('API Configuration:', API_URLS);
