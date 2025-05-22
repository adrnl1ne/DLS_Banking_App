import axios from 'axios';
import axiosInstance from './axiosConfig';

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
    const response = await axiosInstance.post(`${API_URL}/users`, user);
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
    const response = await axiosInstance.get(`${API_URL}/users`);
    return response.data;
  } catch (error) {
    if (axios.isAxiosError(error)) {
      throw new Error(error.response?.data || 'Failed to fetch users');
    }
    throw new Error('An unexpected error occurred');
  }
};


