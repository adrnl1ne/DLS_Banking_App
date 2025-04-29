import { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { getUserAccounts, Account } from '../api/accountApi';
import { getCurrentUser } from '../api/authApi';
import { Link } from 'react-router-dom';

const Dashboard = () => {
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [totalBalance, setTotalBalance] = useState(0);
  const user = getCurrentUser()?.user;

  useEffect(() => {
    const fetchAccounts = async () => {
      try {
        setIsLoading(true);
        const data = await getUserAccounts();
        setAccounts(data);
        
        // Calculate total balance across all accounts
        const total = data.reduce((sum, account) => sum + account.amount, 0);
        setTotalBalance(total);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load accounts');
      } finally {
        setIsLoading(false);
      }
    };

    fetchAccounts();
  }, []);

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
    }).format(amount);
  };

  return (
    <div className="space-y-8">
      <div className="flex flex-col space-y-2 mb-8">
        <h1 className="text-3xl font-bold text-primary">Welcome, {user?.email || 'User'}</h1>
        <p className="text-muted-foreground">Here's a summary of your financial status</p>
      </div>

      {error && (
        <div className="error-message px-4 py-3 rounded-md mb-6" role="alert">
          <span className="font-bold">Error:</span> {error}
        </div>
      )}

      <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
        <Card className="card md:col-span-2">
          <CardHeader className="pb-2">
            <CardTitle className="text-xl text-primary">Account Summary</CardTitle>
            <CardDescription>Overview of your financial accounts</CardDescription>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <div className="py-8 flex justify-center items-center">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
              </div>
            ) : (
              <>
                <div className="mb-8 mt-4 bg-secondary/30 p-6 rounded-lg">
                  <p className="text-sm text-muted-foreground mb-1">Total Balance</p>
                  <h2 className="text-4xl font-bold text-foreground">{formatCurrency(totalBalance)}</h2>
                </div>
                
                <div className="space-y-6">
                  <div className="flex justify-between items-center border-b border-border pb-4">
                    <span className="font-medium">Total Accounts</span>
                    <span className="text-lg font-semibold">{accounts.length}</span>
                  </div>
                  
                  {accounts.slice(0, 3).map((account) => (
                    <div key={account.id} className="account-item">
                      <div className="flex justify-between items-center">
                        <div>
                          <h3 className="font-medium">{account.name}</h3>
                          <p className="text-sm text-muted-foreground account-label">Account #{account.id}</p>
                        </div>
                        <span className="font-semibold">{formatCurrency(account.amount)}</span>
                      </div>
                    </div>
                  ))}
                  
                  <Button asChild variant="outline" className="w-full mt-4">
                    <Link to="/accounts" className="flex items-center justify-center">
                      View All Accounts 
                      <svg className="ml-2 h-4 w-4" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M5 12h14"></path>
                        <path d="M12 5l7 7-7 7"></path>
                      </svg>
                    </Link>
                  </Button>
                </div>
              </>
            )}
          </CardContent>
        </Card>
        
        <Card className="card">
          <CardHeader className="pb-2">
            <CardTitle className="text-xl text-primary">Quick Actions</CardTitle>
            <CardDescription>Manage your banking tasks</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4 pt-6">
            <Button asChild className="w-full flex items-center justify-start h-12 px-4">
              <Link to="/accounts">
                <svg className="mr-3 h-5 w-5" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect>
                  <line x1="3" y1="9" x2="21" y2="9"></line>
                  <line x1="9" y1="21" x2="9" y2="9"></line>
                </svg>
                Manage Accounts
              </Link>
            </Button>
            <Button asChild variant="outline" className="w-full flex items-center justify-start h-12 px-4">
              <Link to="/transactions">
                <svg className="mr-3 h-5 w-5" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="12" y1="1" x2="12" y2="23"></line>
                  <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
                </svg>
                View Transactions
              </Link>
            </Button>
            <Button asChild variant="outline" className="w-full flex items-center justify-start h-12 px-4">
              <Link to="#">
                <svg className="mr-3 h-5 w-5" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="12" r="10"></circle>
                  <line x1="12" y1="8" x2="12" y2="16"></line>
                  <line x1="8" y1="12" x2="16" y2="12"></line>
                </svg>
                Transfer Money
              </Link>
            </Button>
          </CardContent>
        </Card>
      </div>
      
      <Card className="card mt-8">
        <CardHeader className="pb-2">
          <CardTitle className="text-xl text-primary">Recent Activity</CardTitle>
          <CardDescription>Your latest transactions and updates</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="text-center py-12 text-muted-foreground flex flex-col items-center">
            <div className="bg-secondary/40 inline-flex items-center justify-center w-16 h-16 rounded-full mb-4">
              <svg className="h-8 w-8 text-muted-foreground" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="10"></circle>
                <line x1="12" y1="8" x2="12" y2="12"></line>
                <line x1="12" y1="16" x2="12.01" y2="16"></line>
              </svg>
            </div>
            <p className="text-lg mb-2">No recent activity</p>
            <p className="max-w-md">Your recent account activity will be shown here when you start making transactions.</p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
};

export default Dashboard; 