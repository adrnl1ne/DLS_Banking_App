import axios from 'axios';

// Define API URL
const API_URL = 'http://localhost:8002/api/Account';

// Define interfaces
export interface Account {
  id: number;
  name: string;
  amount: number;
  userId: number;
}

export interface AccountCreationRequest {
  name: string;
}

export interface DepositRequest {
  amount: number;
}

// Get an account by ID
export const getAccount = async (id: number): Promise<Account> => {
  try {
    const token = localStorage.getItem('token');
    if (!token) {
      throw new Error('Authentication required');
    }

    const response = await axios.get(`${API_URL}/${id}`, {
      headers: {
        Authorization: `Bearer ${token}`
      }
    });
    
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
    const token = localStorage.getItem('token');
    if (!token) {
      throw new Error('Authentication required');
    }

    const response = await axios.post(API_URL, accountData, {
      headers: {
        Authorization: `Bearer ${token}`
      }
    });
    
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
    const token = localStorage.getItem('token');
    if (!token) {
      throw new Error('Authentication required');
    }

    const response = await axios.get(`${API_URL}/user`, {
      headers: {
        Authorization: `Bearer ${token}`
      }
    });
    
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
    const token = localStorage.getItem('token');
    if (!token) {
      throw new Error('Authentication required');
    }

    const response = await axios.post(`${API_URL}/${accountId}/deposit`, depositRequest, {
      headers: {
        Authorization: `Bearer ${token}`
      }
    });
    return response.data;
  } catch (error) {
    if (axios.isAxiosError(error)) {
      if (error.response?.status === 401) {
        throw new Error('Authentication required');
      }
      throw new Error(error.response?.data?.message || 'Failed to deposit to account');
    }
    throw error;
  }
};
