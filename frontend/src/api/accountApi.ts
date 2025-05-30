import axios from 'axios';
import axiosInstance from './axiosConfig';
import { API_URLS } from '../config/apiConfig';

// Use dynamic API URL
const API_URL = API_URLS.ACCOUNT;

// Define interfaces
export interface Account {
  id: number;
  name: string;
  amount: number;
  userId: number;
  createdAt: string;
  updatedAt: string;
}

export interface AccountCreationRequest {
  name: string;
  userId: number;
}

export interface DepositRequest {
  amount: number;
}

// Get an account by ID
export const getAccount = async (id: number): Promise<Account> => {
  try {
    const response = await axiosInstance.get(`${API_URL}/${id}`);
    return response.data;
  } catch (error) {
    if (axios.isAxiosError(error) && error.response) {
      throw new Error(error.response.data || 'Failed to fetch account');
    }
    throw new Error('An unexpected error occurred');
  }
};

// Create a new account
export const createAccount = async (accountData: AccountCreationRequest): Promise<Account> => {
  try {
    const response = await axiosInstance.post(API_URL, accountData);
    return response.data;
  } catch (error) {
    if (axios.isAxiosError(error) && error.response) {
      throw new Error(error.response.data || 'Failed to create account');
    }
    throw new Error('An unexpected error occurred');
  }
};

export const getUserAccounts = async (): Promise<Account[]> => {
  try {
    const response = await axiosInstance.get(`${API_URL}/user`);
    return response.data;
  } catch (error) {
    if (axios.isAxiosError(error) && error.response) {
      throw new Error(error.response.data || 'Failed to fetch user accounts');
    }
    throw new Error('An unexpected error occurred');
  }
};

export const depositToAccount = async (accountId: number, depositRequest: DepositRequest): Promise<Account> => {
  try {
    const response = await axiosInstance.post(`${API_URL}/${accountId}/deposit`, depositRequest);
    return response.data;
  } catch (error) {
    if (axios.isAxiosError(error)) {
      throw new Error(error.response?.data?.message || 'Failed to deposit to account');
    }
    throw error;
  }
};
