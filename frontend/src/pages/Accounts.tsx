import { useState, useEffect } from 'react';
import { Card, CardContent, CardFooter, CardHeader, CardTitle, CardDescription } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Separator } from '../components/ui/separator';
import { Alert, AlertDescription } from '../components/ui/alert';
import { useNavigate } from 'react-router-dom';
import { getAccounts } from '../api/graphqlApi';
import type { AccountEvent } from '../api/types';

const Accounts = () => {
  const [accounts, setAccounts] = useState<AccountEvent[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, ] = useState('');
  const navigate = useNavigate();

  useEffect(() => {
    const fetchAccounts = async () => {
      try {
        setIsLoading(true);
        setError('');
        const data = await getAccounts();
        console.log('Account data:', data);
        
        // Filter for only the latest account events
        const latestAccounts = data.reduce((acc: { [key: number]: AccountEvent }, event: AccountEvent) => {
          if (!acc[event.accountId] || new Date(event.timestamp) > new Date(acc[event.accountId].timestamp)) {
            acc[event.accountId] = event;
          }
          return acc;
        }, {});

        setAccounts(Object.values(latestAccounts));
      } catch (err) {
        console.error('Error fetching accounts:', err);
        
        // Handle specific error cases
        if (err instanceof Error) {
          if (err.message.includes('Authentication required')) {
            setError('Authentication required. Please log in again.');
            navigate('/login');
          } else if (err.message.includes('Failed to fetch')) {
            setError('Could not load accounts. Please try again later.');
          } else {
            setError(err.message);
          }
        } else {
          setError('An unexpected error occurred');
        }
      } finally {
        setIsLoading(false);
      }
    };

    fetchAccounts();
  }, [navigate]);

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
    }).format(amount);
  };

  return (
    <div className="space-y-8">
      <div className="flex flex-col md:flex-row md:justify-between md:items-center gap-4">
        <div className="space-y-2">
          <h1 className="text-2xl font-bold text-primary">My Accounts</h1>
          <p className="text-muted-foreground">View your financial accounts</p>
        </div>
      </div>
      
      {success && (
        <Alert className="border-green-200 bg-green-50 text-green-700">
          <div className="flex items-center gap-2">
            <svg className="h-4 w-4" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
              <polyline points="22 4 12 14.01 9 11.01"></polyline>
            </svg>
            <AlertDescription>{success}</AlertDescription>
          </div>
        </Alert>
      )}
      
      {error && (
        <Alert className="border-red-200 bg-red-50 text-red-700">
          <div className="flex items-center gap-2">
            <svg className="h-4 w-4" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="12" y1="8" x2="12" y2="12"></line>
              <line x1="12" y1="16" x2="12.01" y2="16"></line>
            </svg>
            <AlertDescription>{error}</AlertDescription>
          </div>
        </Alert>
      )}
      
      {isLoading ? (
        <div className="py-16 flex justify-center items-center">
          <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-primary"></div>
        </div>
      ) : accounts.length === 0 ? (
        <Card className="card">
          <CardContent className="flex flex-col items-center justify-center py-16">
            <div className="bg-secondary/40 inline-flex items-center justify-center w-16 h-16 rounded-full mb-4">
              <svg className="h-8 w-8 text-muted-foreground" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect>
                <line x1="3" y1="9" x2="21" y2="9"></line>
                <line x1="9" y1="21" x2="9" y2="9"></line>
              </svg>
            </div>
            <h3 className="text-xl font-medium mb-2">You don't have any accounts yet</h3>
            <p className="text-muted-foreground text-center max-w-md mb-6">Please contact an administrator to create an account for you.</p>
          </CardContent>
        </Card>
      ) :
        <div className="grid gap-6">
          {accounts.map(account => (
            <Card key={account.accountId} className="card overflow-hidden">
              <CardHeader className="pb-2">
                <CardTitle className="text-xl text-primary">{account.name || `Account ${account.accountId}`}</CardTitle>
                <CardDescription>Account #{account.accountId}</CardDescription>
              </CardHeader>
              <CardContent className="pt-4">
                <div className="flex flex-col md:flex-row justify-between md:items-start mb-6">
                  <div className="space-y-2">
                    <div className="text-sm text-muted-foreground">Available Balance</div>
                    <div className="text-3xl font-bold">{formatCurrency(account.amount || 0)}</div>
                  </div>
                  <div className="mt-4 md:mt-0 self-start bg-secondary/30 px-4 py-2 rounded-md">
                    <div className="text-sm text-muted-foreground">User ID</div>
                    <div className="font-medium">{account.userId}</div>
                  </div>
                </div>
              </CardContent>
              
              <Separator className="bg-border/60" />
              
              <CardFooter className="pt-6">
                <Button 
                  variant="outline" 
                  className="w-full justify-center"
                  onClick={() => navigate(`/accounts/${account.accountId}`)}
                >
                  View Details
                </Button>
              </CardFooter>
            </Card>
          ))}
        </div>
      }
    </div>
  );
};

export default Accounts; 