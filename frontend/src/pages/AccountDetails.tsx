import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { getUserAccounts, Account, depositToAccount, DepositRequest } from '../api/accountApi';
import { 
  Dialog, 
  DialogContent, 
  DialogHeader, 
  DialogTitle, 
  DialogFooter,
  DialogTrigger 
} from '../components/ui/dialog';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Alert } from '../components/ui/alert';

const AccountDetails = () => {
  const { accountId } = useParams();
  const navigate = useNavigate();
  const [account, setAccount] = useState<Account | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [depositAmount, setDepositAmount] = useState('');
  const [isDepositing, setIsDepositing] = useState(false);
  const [depositDialogOpen, setDepositDialogOpen] = useState(false);
  const [depositError, setDepositError] = useState('');
  const [depositSuccess, setDepositSuccess] = useState('');

  useEffect(() => {
    const fetchAccountDetails = async () => {
      try {
        setIsLoading(true);
        setError('');
        const accounts = await getUserAccounts();
        const foundAccount = accounts.find(acc => acc.id === Number(accountId));
        
        if (!foundAccount) {
          setError('Account not found');
          return;
        }
        
        setAccount(foundAccount);
      } catch (err) {
        console.error('Error fetching account details:', err);
        if (err instanceof Error) {
          if (err.message.includes('Authentication required')) {
            setError('Authentication required. Please log in again.');
            navigate('/login');
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

    fetchAccountDetails();
  }, [accountId, navigate]);

  const handleDeposit = async () => {
    if (!account) return;
    
    const amount = parseFloat(depositAmount);
    if (isNaN(amount) || amount <= 0) {
      setDepositError('Please enter a valid amount greater than 0');
      return;
    }

    try {
      setIsDepositing(true);
      setDepositError('');
      setDepositSuccess('');
      
      const depositRequest: DepositRequest = { amount };
      const updatedAccount = await depositToAccount(account.id, depositRequest);
      
      setAccount(updatedAccount);
      setDepositSuccess(`Successfully deposited ${formatCurrency(amount)}`);
      setDepositAmount('');
      setDepositDialogOpen(false);
      
      // Clear success message after 5 seconds
      setTimeout(() => setDepositSuccess(''), 5000);
    } catch (err) {
      console.error('Error depositing to account:', err);
      if (err instanceof Error) {
        setDepositError(err.message);
      } else {
        setDepositError('An unexpected error occurred');
      }
    } finally {
      setIsDepositing(false);
    }
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
    }).format(amount);
  };

  if (isLoading) {
    return (
      <div className="py-16 flex justify-center items-center">
        <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-primary"></div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="space-y-4">
        <Button variant="outline" onClick={() => navigate('/accounts')} className="flex items-center">
          <svg className="mr-2 h-4 w-4" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M19 12H5M12 19l-7-7 7-7"/>
          </svg>
          Back to Accounts
        </Button>
        <Alert className="border-red-200 bg-red-50 text-red-700 p-4">
          <div className="flex items-center gap-2">
            <svg className="h-5 w-5" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="12" y1="8" x2="12" y2="12"></line>
              <line x1="12" y1="16" x2="12.01" y2="16"></line>
            </svg>
            <p>{error}</p>
          </div>
        </Alert>
      </div>
    );
  }

  if (!account) {
    return (
      <div className="space-y-4">
        <Button variant="outline" onClick={() => navigate('/accounts')} className="flex items-center">
          <svg className="mr-2 h-4 w-4" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M19 12H5M12 19l-7-7 7-7"/>
          </svg>
          Back to Accounts
        </Button>
        <Card className="card">
          <CardContent className="flex flex-col items-center justify-center py-16">
            <div className="bg-secondary/40 inline-flex items-center justify-center w-16 h-16 rounded-full mb-4">
              <svg className="h-8 w-8 text-muted-foreground" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="10"></circle>
                <line x1="12" y1="8" x2="12" y2="12"></line>
                <line x1="12" y1="16" x2="12.01" y2="16"></line>
              </svg>
            </div>
            <h3 className="text-xl font-medium mb-2">Account not found</h3>
            <p className="text-muted-foreground text-center">The account you're looking for might have been deleted or doesn't exist.</p>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <div className="flex flex-col md:flex-row md:justify-between md:items-center gap-4">
        <div className="flex items-center">
          <Button variant="outline" onClick={() => navigate('/accounts')} className="flex items-center mr-4">
            <svg className="mr-2 h-4 w-4" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M19 12H5M12 19l-7-7 7-7"/>
            </svg>
            Back
          </Button>
          <div>
            <h1 className="text-2xl font-bold text-primary">{account.name}</h1>
            <p className="text-muted-foreground">Account #{account.id}</p>
          </div>
        </div>
      </div>

      {depositSuccess && (
        <Alert className="border-green-200 bg-green-50 text-green-700 p-4">
          <div className="flex items-center gap-2">
            <svg className="h-5 w-5" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
              <polyline points="22 4 12 14.01 9 11.01"></polyline>
            </svg>
            <p>{depositSuccess}</p>
          </div>
        </Alert>
      )}

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Card className="card md:col-span-2">
          <CardHeader className="pb-2">
            <CardTitle className="text-xl text-primary">Account Details</CardTitle>
            <CardDescription>Overview of your account information</CardDescription>
          </CardHeader>
          <CardContent className="pt-4">
            <div className="bg-secondary/30 p-6 rounded-lg mb-6">
              <p className="text-sm text-muted-foreground mb-1">Current Balance</p>
              <h2 className="text-4xl font-bold">{formatCurrency(account.amount)}</h2>
            </div>
            
            <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
              <div className="space-y-6">
                <div>
                  <h3 className="text-sm font-medium text-muted-foreground mb-1">Account Name</h3>
                  <p className="text-lg font-medium">{account.name}</p>
                </div>
                <div>
                  <h3 className="text-sm font-medium text-muted-foreground mb-1">Account ID</h3>
                  <p className="text-lg">{account.id}</p>
                </div>
              </div>
              <div className="space-y-6">
                <div>
                  <h3 className="text-sm font-medium text-muted-foreground mb-1">User ID</h3>
                  <p className="text-lg">{account.userId}</p>
                </div>
                <div>
                  <h3 className="text-sm font-medium text-muted-foreground mb-1">Account Status</h3>
                  <div className="inline-flex items-center px-2.5 py-0.5 rounded-full text-sm font-medium bg-green-50 text-green-700 border border-green-200">
                    Active
                  </div>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
        
        <Card className="card">
          <CardHeader className="pb-2">
            <CardTitle className="text-xl text-primary">Quick Actions</CardTitle>
            <CardDescription>Manage your account</CardDescription>
          </CardHeader>
          <CardContent className="pt-4 space-y-4">
            <Dialog open={depositDialogOpen} onOpenChange={setDepositDialogOpen}>
              <DialogTrigger asChild>
                <Button className="w-full button-primary flex items-center justify-start h-12 px-4">
                  <svg className="mr-3 h-5 w-5" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <line x1="12" y1="1" x2="12" y2="23"></line>
                    <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
                  </svg>
                  Deposit Money
                </Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle className="text-primary">Deposit to {account.name}</DialogTitle>
                </DialogHeader>
                <div className="grid gap-4 py-4">
                  <div className="grid grid-cols-4 items-center gap-4">
                    <Label htmlFor="amount" className="text-right text-foreground/90">
                      Amount
                    </Label>
                    <Input
                      id="amount"
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={depositAmount}
                      onChange={(e) => setDepositAmount(e.target.value)}
                      className="col-span-3 input-large"
                      placeholder="Enter amount to deposit"
                    />
                  </div>
                  {depositError && (
                    <div className="text-red-500 text-sm flex items-center gap-2">
                      <svg className="h-4 w-4" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <circle cx="12" cy="12" r="10"></circle>
                        <line x1="12" y1="8" x2="12" y2="12"></line>
                        <line x1="12" y1="16" x2="12.01" y2="16"></line>
                      </svg>
                      {depositError}
                    </div>
                  )}
                </div>
                <DialogFooter>
                  <Button 
                    onClick={handleDeposit} 
                    disabled={isDepositing || !depositAmount || parseFloat(depositAmount) <= 0}
                    className="button-primary"
                  >
                    {isDepositing ? 'Depositing...' : 'Deposit'}
                  </Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
            
            <Button variant="outline" className="w-full flex items-center justify-start h-12 px-4">
              <svg className="mr-3 h-5 w-5" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="6" y="4" width="12" height="16" rx="2"></rect>
                <line x1="12" y1="2" x2="12" y2="4"></line>
                <line x1="12" y1="20" x2="12" y2="22"></line>
                <line x1="10" y1="12" x2="14" y2="12"></line>
              </svg>
              Transfer Funds
            </Button>
            
            <Button variant="outline" className="w-full flex items-center justify-start h-12 px-4">
              <svg className="mr-3 h-5 w-5" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
                <polyline points="14 2 14 8 20 8"></polyline>
                <line x1="16" y1="13" x2="8" y2="13"></line>
                <line x1="16" y1="17" x2="8" y2="17"></line>
                <polyline points="10 9 9 9 8 9"></polyline>
              </svg>
              View Statements
            </Button>
          </CardContent>
        </Card>
      </div>
    </div>
  );
};

export default AccountDetails; 