import { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { getUserAccounts, Account } from '../api/accountApi';
import { getCurrentUser } from '../api/authApi';
import { Link } from 'react-router-dom';
import { ArrowRight } from 'lucide-react';

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
      <div className="flex justify-between items-center">
        <h1 className="text-3xl font-bold">Welcome, {user?.firstName || 'User'}</h1>
      </div>

      {error && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded mb-6" role="alert">
          <span className="font-bold">Error:</span> {error}
        </div>
      )}

      <div className="grid gap-6 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Account Summary</CardTitle>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <div className="py-4">Loading...</div>
            ) : (
              <>
                <div className="mb-6">
                  <p className="text-sm text-muted-foreground">Total Balance</p>
                  <h2 className="text-3xl font-bold">{formatCurrency(totalBalance)}</h2>
                </div>
                
                <div className="space-y-4">
                  <div className="flex justify-between">
                    <span>Total Accounts</span>
                    <span className="font-medium">{accounts.length}</span>
                  </div>
                  <Button asChild variant="outline" className="w-full">
                    <Link to="/accounts" className="flex items-center justify-center">
                      View All Accounts <ArrowRight className="ml-2 h-4 w-4" />
                    </Link>
                  </Button>
                </div>
              </>
            )}
          </CardContent>
        </Card>
        
        <Card>
          <CardHeader>
            <CardTitle>Quick Actions</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <Button asChild className="w-full">
              <Link to="/accounts">Manage Accounts</Link>
            </Button>
            <Button asChild variant="outline" className="w-full">
              <Link to="/transactions">View Transactions</Link>
            </Button>
            <Button asChild variant="outline" className="w-full">
              <Link to="#">Transfer Money</Link>
            </Button>
          </CardContent>
        </Card>
      </div>
      
      <Card>
        <CardHeader>
          <CardTitle>Recent Activity</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-center py-8 text-muted-foreground">
            Your recent account activity will be shown here.
          </p>
        </CardContent>
      </Card>
    </div>
  );
};

export default Dashboard; 