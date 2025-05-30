import axios from 'axios';
import axiosInstance from './axiosConfig';
import { API_URLS } from '../config/apiConfig';

// Use dynamic API URL
const API_URL = API_URLS.AUTH;

interface LoginCredentials {
  email: string;
  password: string;
}

interface LoginResponse {
  token: string;
  user: User;
}

interface RegisterCredentials {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  password: string;
  confirmPassword: string;
  acceptTerms: boolean;
}

interface RegisterResponse {
  token: string;
  user: User;
}

interface User {
  id: string;
  email: string;
  firstName?: string;
  lastName?: string;
  role?: string;
}

export const login = async (credentials: LoginCredentials): Promise<LoginResponse> => {
  try {
    // Match the backend endpoint from AuthService.cs
    const response = await axiosInstance.post(`${API_URL}/login`, {
      usernameOrEmail: credentials.email,
      password: credentials.password
    });
    
    // The backend returns { Token: tokenString }
    const token = response.data.Token || response.data.token;
    
    if (!token) {
      throw new Error('No token received from server');
    }
    
    // Parse the JWT token to get user information
    const user = parseJwt(token);
    
    // Store in localStorage
    localStorage.setItem('token', token);
    localStorage.setItem('user', JSON.stringify({
      id: user.id,
      email: user.email,
      role: user.role
    }));
    
    return {
      token,
      user
    };
  } catch (error) {
    if (axios.isAxiosError(error) && error.response) {
      throw new Error(error.response.data || 'Failed to login');
    }
    throw new Error('An unexpected error occurred');
  }
};

export const register = async (credentials: RegisterCredentials): Promise<RegisterResponse> => {
  try {
    const response = await axiosInstance.post(`${API_URL}/register`, credentials);
    return response.data;
  } catch (error) {
    if (axios.isAxiosError(error) && error.response) {
      throw new Error(error.response.data || 'Failed to register');
    }
    throw new Error('An unexpected error occurred');
  }
};


// Helper function to parse JWT token
const parseJwt = (token: string): User => {
  try {
    const base64Url = token.split('.')[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    const payload = JSON.parse(jsonPayload);
    
    // The user ID is stored in 'sub' claim or the nameidentifier claim
    const userId = payload.sub || 
                  payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"];
    
    return {
      id: userId,
      email: payload.email,
      firstName: payload.given_name,
      lastName: payload.family_name,
      role: payload.role
    };
  } catch (e) {
    console.error('Error parsing JWT token:', e);
    throw new Error('Invalid token format');
  }
};

export const logout = (): void => {
  localStorage.removeItem('token');
  localStorage.removeItem('user');
};

export const getCurrentUser = (): { token: string; user: User } | null => {
  const token = localStorage.getItem('token');
  const user = localStorage.getItem('user');
  
  if (token && user) {
    return {
      token,
      user: JSON.parse(user)
    };
  }
  
  return null;
};

// Add a function to get the current user ID directly
export const getCurrentUserId = (): number | null => {
  const user = localStorage.getItem('user');
  if (user) {
    const userData = JSON.parse(user);
    return userData.id ? Number(userData.id) : null;
  }
  return null;
};

export const isAuthenticated = (): boolean => {
  return localStorage.getItem('token') !== null;
};
