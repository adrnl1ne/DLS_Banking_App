import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';
import { Label } from '../components/ui/label';

interface Transaction {
  id: number;
  date: string;
  description: string;
  category: string;
  amount: number;
  type: 'credit' | 'debit';
  account: string;
  balance: number;
}

const Transactions = () => {
  const [transactions] = useState<Transaction[]>([
    {
      id: 1,
      date: '2025-04-20',
      description: 'Grocery Store',
      category: 'Groceries',
      amount: 78.35,
      type: 'debit',
      account: 'Checking Account (**** 4567)',
      balance: 2540.80
    },
    {
      id: 2,
      date: '2025-04-19',
      description: 'Salary Deposit',
      category: 'Income',
      amount: 3500.00,
      type: 'credit',
      account: 'Checking Account (**** 4567)',
      balance: 2619.15
    },
    {
      id: 3,
      date: '2025-04-18',
      description: 'Coffee Shop',
      category: 'Dining',
      amount: 4.50,
      type: 'debit',
      account: 'Checking Account (**** 4567)',
      balance: -880.85
    },
    {
      id: 4,
      date: '2025-04-17',
      description: 'Electric Bill',
      category: 'Utilities',
      amount: 65.78,
      type: 'debit',
      account: 'Checking Account (**** 4567)',
      balance: -876.35
    },
    {
      id: 5,
      date: '2025-04-16',
      description: 'Transfer to Savings',
      category: 'Transfer',
      amount: 500.00,
      type: 'debit',
      account: 'Checking Account (**** 4567)',
      balance: -810.57
    },
    {
      id: 6,
      date: '2025-04-16',
      description: 'Transfer from Checking',
      category: 'Transfer',
      amount: 500.00,
      type: 'credit',
      account: 'Savings Account (**** 7890)',
      balance: 15750.25
    },
  ]);

  const [filterAccount, setFilterAccount] = useState<string>('all');
  const [filterType, setFilterType] = useState<string>('all');

  const filteredTransactions = transactions.filter(transaction => {
    if (filterAccount !== 'all' && !transaction.account.includes(filterAccount)) {
      return false;
    }
    if (filterType !== 'all' && transaction.type !== filterType) {
      return false;
    }
    return true;
  });

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Transaction History</h1>
      
      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div className="space-y-2">
              <Label htmlFor="account">Account</Label>
              <select 
                id="account"
                className="w-full p-2 border border-input rounded-md"
                value={filterAccount}
                onChange={(e) => setFilterAccount(e.target.value)}
              >
                <option value="all">All Accounts</option>
                <option value="4567">Checking Account (**** 4567)</option>
                <option value="7890">Savings Account (**** 7890)</option>
              </select>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="type">Transaction Type</Label>
              <select 
                id="type"
                className="w-full p-2 border border-input rounded-md"
                value={filterType}
                onChange={(e) => setFilterType(e.target.value)}
              >
                <option value="all">All Transactions</option>
                <option value="credit">Deposits</option>
                <option value="debit">Withdrawals</option>
              </select>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="date-range">Date Range</Label>
              <select 
                id="date-range"
                className="w-full p-2 border border-input rounded-md"
              >
                <option>Last 30 days</option>
                <option>Last 90 days</option>
                <option>Year to date</option>
                <option>Custom range</option>
              </select>
            </div>
          </div>
        </CardContent>
      </Card>
      
      <Card>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Date</TableHead>
                <TableHead>Description</TableHead>
                <TableHead>Category</TableHead>
                <TableHead>Account</TableHead>
                <TableHead className="text-right">Amount</TableHead>
                <TableHead className="text-right">Balance</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredTransactions.map(transaction => (
                <TableRow key={transaction.id}>
                  <TableCell>{transaction.date}</TableCell>
                  <TableCell>{transaction.description}</TableCell>
                  <TableCell>
                    <span className="inline-block px-2 py-1 bg-secondary text-secondary-foreground text-xs rounded-full">
                      {transaction.category}
                    </span>
                  </TableCell>
                  <TableCell>{transaction.account}</TableCell>
                  <TableCell className={`text-right ${
                    transaction.type === 'credit' ? 'text-green-600' : 'text-red-600'
                  }`}>
                    {transaction.type === 'credit' ? '+' : '-'}${transaction.amount.toFixed(2)}
                  </TableCell>
                  <TableCell className="text-right">${transaction.balance.toFixed(2)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
};

export default Transactions; 