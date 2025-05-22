import axios from 'axios';

// Define API URL
const API_URL = 'http://localhost:8002/api/Token';

export interface User {
  id: number;
  username: string;
  email: string;
  role: string;
  createdAt: string;
  updatedAt: string;
}

export interface UserRequest {
  username: string;
  email: string;
  password: string;
  roleId: number;
}

export const createUser = async (user: UserRequest): Promise<User> => {
  try {
    const token = localStorage.getItem('token');
    if (!token) {
      throw new Error('Authentication required');
    }
    const response = await axios.post(`${API_URL}/users`, user, {
      headers: {
        Authorization: `Bearer ${token}`
      }
    });
    return response.data;
  }
  catch (error) {
    if (axios.isAxiosError(error)) {
      throw new Error(error.response?.data || 'Failed to create user');
    }
    throw new Error('An unexpected error occurred');
  }
};

export const getUsers = async (): Promise<User[]> => {
  try {
    const token = localStorage.getItem('token');
    if (!token) {
      throw new Error('Authentication required');
    }

    const response = await axios.get(`${API_URL}/users`, {
      headers: {
        Authorization: `Bearer ${token}`
      }
    });
    
    return response.data;
  } catch (error) {
    if (axios.isAxiosError(error)) {
      if (error.response?.status === 401) {
        throw new Error('Authentication required');
      } else if (error.response?.status === 403) {
        throw new Error('You do not have permission to view users');
      }
      throw new Error(error.response?.data || 'Failed to fetch users');
    }
    throw new Error('An unexpected error occurred');
  }
};


