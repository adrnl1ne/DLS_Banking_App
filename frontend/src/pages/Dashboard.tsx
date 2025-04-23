import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';

interface AccountSummary {
  id: number;
  name: string;
  balance: number;
  accountNumber: string;
}

interface Transaction {
  id: number;
  date: string;
  description: string;
  amount: number;
  type: 'credit' | 'debit';
}

const Dashboard = () => {
  const [accounts] = useState<AccountSummary[]>([
    { id: 1, name: 'Checking Account', balance: 2540.80, accountNumber: '**** 4567' },
    { id: 2, name: 'Savings Account', balance: 15750.25, accountNumber: '**** 7890' },
  ]);
  
  const [recentTransactions] = useState<Transaction[]>([
    { id: 1, date: '2025-04-20', description: 'Grocery Store', amount: 78.35, type: 'debit' },
    { id: 2, date: '2025-04-19', description: 'Salary Deposit', amount: 3500.00, type: 'credit' },
    { id: 3, date: '2025-04-18', description: 'Coffee Shop', amount: 4.50, type: 'debit' },
    { id: 4, date: '2025-04-17', description: 'Electric Bill', amount: 65.78, type: 'debit' },
  ]);

  return (
    <div className="space-y-8">
      <h1 className="text-3xl font-bold title-primary">Dashboard</h1>
      
      <div className="grid gap-6 md:grid-cols-2">
        <Card className="shadow-md">
          <CardHeader className="card-header-muted">
            <CardTitle className="card-title-primary">Account Summary</CardTitle>
          </CardHeader>
          <CardContent className="card-content-padded">
            {accounts.map(account => (
              <div key={account.id} className="account-item">
                <div className="flex justify-between mb-1">
                  <span className="font-medium">{account.name}</span>
                  <span className="font-bold">${account.balance.toFixed(2)}</span>
                </div>
                <div className="account-label">
                  Account: {account.accountNumber}
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
        
        <Card className="shadow-md">
          <CardHeader className="card-header-muted">
            <CardTitle className="card-title-primary">Quick Actions</CardTitle>
          </CardHeader>
          <CardContent className="card-content-padded">
            <div className="grid grid-cols-2 gap-4">
              <Button className="button-primary">
                Transfer Money
              </Button>
              <Button className="button-primary">
                Pay Bills
              </Button>
              <Button className="button-primary">
                Mobile Deposit
              </Button>
              <Button className="button-primary">
                Apply for Credit
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
      
      <Card className="shadow-md">
        <CardHeader className="card-header-muted">
          <CardTitle className="card-title-primary">Recent Transactions</CardTitle>
        </CardHeader>
        <CardContent className="card-content-padded">
          <Table>
            <TableHeader>
              <TableRow className="table-header-muted">
                <TableHead>Date</TableHead>
                <TableHead>Description</TableHead>
                <TableHead className="text-right">Amount</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {recentTransactions.map(transaction => (
                <TableRow key={transaction.id} className="hover:bg-muted/30">
                  <TableCell>{transaction.date}</TableCell>
                  <TableCell>{transaction.description}</TableCell>
                  <TableCell className={transaction.type === 'credit' ? 'amount-credit' : 'amount-debit'}>
                    {transaction.type === 'credit' ? '+' : '-'}${transaction.amount.toFixed(2)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
};

export default Dashboard; 