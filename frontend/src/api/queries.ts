import { gql } from '@apollo/client';

export const GET_ACCOUNTS = gql`
  query GetAccounts($userId: Int) {
    accounts(userId: $userId) {
      eventType
      accountId
      userId
      name
      amount
      transactionId
      transactionType
      timestamp
    }
  }
`;

export const GET_ACCOUNT_HISTORY = gql`
  query GetAccountHistory($accountId: Int!) {
    accountHistory(accountId: $accountId) {
      eventType
      accountId
      userId
      name
      amount
      transactionId
      transactionType
      timestamp
    }
  }
`;

export const GET_TRANSACTIONS = gql`
  query GetTransactions($accountId: String) {
    transactions(accountId: $accountId) {
      transferId
      fromAccount
      toAccount
      amount
      timestamp
      status
    }
  }
`;
