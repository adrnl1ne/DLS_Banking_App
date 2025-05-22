import { apolloClient } from './graphqlClient';
import {
  GET_ACCOUNTS,
  GET_ACCOUNT_HISTORY,
  GET_TRANSACTIONS,
} from './queries';
import type {
  AccountEvent,
  TransactionEvent,
} from './types';

export const getAccounts = async (userId?: number) => {
  const { data } = await apolloClient.query<{ accounts: AccountEvent[] }>({
    query: GET_ACCOUNTS,
    variables: { userId },
  });
  return data.accounts;
};

export const getAccountHistory = async (accountId: number) => {
  const { data } = await apolloClient.query<{ accountHistory: AccountEvent[] }>({
    query: GET_ACCOUNT_HISTORY,
    variables: { accountId },
  });
  return data.accountHistory;
};

export const getTransactions = async (accountId?: string) => {
  const { data } = await apolloClient.query<{ transactions: TransactionEvent[] }>({
    query: GET_TRANSACTIONS,
    variables: { accountId },
  });
  return data.transactions;
};
