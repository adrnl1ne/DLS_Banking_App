import axios from 'axios';
import { logout } from './authApi';

// Create axios instance
const axiosInstance = axios.create();

// Add a request interceptor
axiosInstance.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Add a response interceptor
axiosInstance.interceptors.response.use(
  (response) => {
    return response;
  },
  (error) => {
    if (error.response) {
      // Handle 401 Unauthorized or 403 Forbidden responses
      if (error.response.status === 401 || error.response.status === 403) {
        // Clear user data and redirect to login
        logout();
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  }
);

export default axiosInstance; 