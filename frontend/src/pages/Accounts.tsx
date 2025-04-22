import { useState } from 'react';
import { Card, CardContent, CardFooter } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Separator } from '../components/ui/separator';

interface Account {
  id: number;
  name: string;
  type: string;
  balance: number;
  accountNumber: string;
  openDate: string;
  interestRate?: number;
  status: 'active' | 'inactive';
}

const Accounts = () => {
  const [accounts] = useState<Account[]>([
    { 
      id: 1, 
      name: 'Checking Account', 
      type: 'Checking',
      balance: 2540.80, 
      accountNumber: '**** 4567',
      openDate: '2022-05-15',
      status: 'active'
    },
    { 
      id: 2, 
      name: 'Savings Account', 
      type: 'Savings',
      balance: 15750.25, 
      accountNumber: '**** 7890',
      openDate: '2022-06-20',
      interestRate: 1.25,
      status: 'active'
    },
    { 
      id: 3, 
      name: 'Joint Checking', 
      type: 'Checking',
      balance: 5230.45, 
      accountNumber: '**** 1234',
      openDate: '2023-01-10',
      status: 'active'
    },
  ]);

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold">My Accounts</h1>
        <Button>
          Open New Account
        </Button>
      </div>
      
      <div className="grid gap-6">
        {accounts.map(account => (
          <Card key={account.id}>
            <CardContent className="pt-6">
              <div className="flex flex-col md:flex-row justify-between md:items-start mb-6">
                <div>
                  <h2 className="text-xl font-semibold">{account.name}</h2>
                  <p className="text-muted-foreground">{account.type} • {account.accountNumber}</p>
                </div>
                <div className="md:text-right mt-2 md:mt-0">
                  <div className="text-2xl font-bold">${account.balance.toFixed(2)}</div>
                  <span className="text-sm text-muted-foreground">Available Balance</span>
                </div>
              </div>
              
              <div className="grid grid-cols-2 md:grid-cols-3 gap-4 text-sm">
                <div>
                  <span className="block text-muted-foreground mb-1">Open Date</span>
                  <span>{account.openDate}</span>
                </div>
                <div>
                  <span className="block text-muted-foreground mb-1">Status</span>
                  <span className={
                    account.status === 'active' ? 'text-green-600' : 'text-red-600'
                  }>
                    {account.status.charAt(0).toUpperCase() + account.status.slice(1)}
                  </span>
                </div>
                {account.interestRate && (
                  <div>
                    <span className="block text-muted-foreground mb-1">Interest Rate</span>
                    <span>{account.interestRate}%</span>
                  </div>
                )}
              </div>
            </CardContent>
            
            <Separator />
            
            <CardFooter className="flex gap-3 pt-6">
              <Button size="sm">
                View Details
              </Button>
              <Button variant="outline" size="sm">
                View Statements
              </Button>
              <Button variant="outline" size="sm">
                Transfer
              </Button>
            </CardFooter>
          </Card>
        ))}
      </div>
    </div>
  );
};

export default Accounts; 