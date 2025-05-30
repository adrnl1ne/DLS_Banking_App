// API Configuration utility that works in any environment
// This dynamically determines the correct API URLs based on the current environment

const getApiBaseUrl = () => {
  // Check if we're in development mode
  if (import.meta.env.DEV) {
    return 'http://localhost';
  }
  
  // In production, use the current hostname (works for any IP/domain)
  const currentHost = window.location.hostname;
  return `http://${currentHost}`;
};

const BASE_URL = getApiBaseUrl();

// API endpoints configuration
export const API_CONFIG = {
  USER_SERVICE: `${BASE_URL}:8002`,
  QUERY_SERVICE: `${BASE_URL}:8004`, 
  TRANSACTION_SERVICE: `${BASE_URL}:8003`,
  
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
