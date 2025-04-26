import axios from 'axios';

// Define API URL
const API_URL = 'http://localhost:8003/api/Transaction';

// Define interfaces
export interface Transaction {
  transferId: string;
  userId: number;
  fromAccount: string;
  toAccount: string;
  amount: number;
  status: string;
  createdAt: string;
  updatedAt?: string;
}

export interface TransactionRequest {
  fromAccount: string;
  toAccount: string;
  amount: number;
}

// Get transactions for a specific account
export const getAccountTransactions = async (accountId: string): Promise<Transaction[]> => {
  try {
    const token = localStorage.getItem('token');
    if (!token) {
      throw new Error('Authentication required');
    }

    const response = await axios.get(`${API_URL}/account/${accountId}`, {
      headers: {
        Authorization: `Bearer ${token}`
      }
    });
    
    return response.data;
  } catch (error) {
    console.error('Error fetching transactions:', error);
    return [];
  }
};

// Get a transaction by ID
export const getTransaction = async (transferId: string): Promise<Transaction | null> => {
  try {
    const token = localStorage.getItem('token');
    if (!token) {
      throw new Error('Authentication required');
    }

    const response = await axios.get(`${API_URL}/${transferId}`, {
      headers: {
        Authorization: `Bearer ${token}`
      }
    });
    
    return response.data;
  } catch (error) {
    console.error('Error fetching transaction:', error);
    return null;
  }
};

// Create a new transaction
export const createTransaction = async (transactionData: TransactionRequest): Promise<Transaction | null> => {
  try {
    const token = localStorage.getItem('token');
    if (!token) {
      throw new Error('Authentication required');
    }

    const response = await axios.post(`${API_URL}/transfer`, transactionData, {
      headers: {
        Authorization: `Bearer ${token}`
      }
    });
    
    return response.data;
  } catch (error) {
    console.error('Error creating transaction:', error);
    
    if (axios.isAxiosError(error) && error.response) {
      throw new Error(error.response.data || 'Failed to create transaction');
    }
    throw new Error('An unexpected error occurred');
  }
}; 