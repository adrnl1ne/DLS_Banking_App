import { useState, useEffect } from 'react';
import { Card, CardContent, CardFooter } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Separator } from '../components/ui/separator';
import { getUserAccounts, Account } from '../api/accountApi';

const Accounts = () => {
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    const fetchAccounts = async () => {
      try {
        setIsLoading(true);
        const data = await getUserAccounts();
        
        setAccounts(data);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load accounts');
      } finally {
        setIsLoading(false);
      }
    };

    fetchAccounts();
  }, []);

  // const handleCreateAccount = async () => {
  //   try {
  //     const newAccount = await createAccount({ name: 'New Account' });
      
  //     // Add display properties to the new account
  //     const enhancedAccount: Account = {
  //       ...newAccount,
  //       type: 'Checking',
  //       accountNumber: `**** ${Math.floor(1000 + Math.random() * 9000)}`,
  //       openDate: new Date().toISOString().split('T')[0],
  //       status: 'active'
  //     };
      
  //     setAccounts([...accounts, enhancedAccount]);
  //   } catch (err) {
  //     setError(err instanceof Error ? err.message : 'Failed to create account');
  //   }
  // };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
    }).format(amount);
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold">My Accounts</h1>
        <Button>
          Open New Account
        </Button>
      </div>
      
      {error && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded mb-6" role="alert">
          <span className="font-bold">Error:</span> {error}
        </div>
      )}
      
      {isLoading ? (
        <div className="text-center py-10">Loading accounts...</div>
      ) : accounts.length === 0 ? (
        <Card>
          <CardContent className="py-10">
            <div className="text-center">
              <h3 className="text-lg font-medium">You don't have any accounts yet</h3>
              <p className="text-gray-500 mt-2">Click the button above to open your first account.</p>
            </div>
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-6">
          {accounts.map(account => (
            <Card key={account.id}>
              <CardContent className="pt-6">
                <div className="flex flex-col md:flex-row justify-between md:items-start mb-6">
                  <div>
                    <h2 className="text-xl font-semibold">{account.name}</h2>
                    {/* <p className="text-muted-foreground">{account.type} • {account.accountNumber}</p> */}
                  </div>
                  <div className="md:text-right mt-2 md:mt-0">
                    <div className="text-2xl font-bold">{formatCurrency(account.amount)}</div>
                    <span className="text-sm text-muted-foreground">Available Balance</span>
                  </div>
                </div>
                
                {/* <div className="grid grid-cols-2 md:grid-cols-3 gap-4 text-sm"> */}
                  {/* <div>
                    <span className="block text-muted-foreground mb-1">Open Date</span>
                    <span>{account.openDate}</span>
                  </div> */}
                  {/* <div>
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
                </div> */}
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
      )}
    </div>
  );
};

export default Accounts; 