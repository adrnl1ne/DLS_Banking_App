export interface AccountEvent {
  eventType: string;
  accountId: number;
  userId: number;
  name?: string;
  amount?: number;
  transactionId?: string;
  transactionType?: string;
  timestamp: string;
}

export interface TransactionEvent {
  transferId: string;
  fromAccount: string;
  toAccount: string;
  amount: number;
  timestamp: string;
  status: string;
}

export interface FraudEvent {
  transferId: string;
  riskScore: number;
  timestamp: string;
  status: string;
}

export interface DeletedAccount {
  accountId: number;
  userId: number;
  timestamp: string;
  reason: string;
} 