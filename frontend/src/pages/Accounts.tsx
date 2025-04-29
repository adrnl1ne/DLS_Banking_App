import { useState, useEffect } from 'react';
import { Card, CardContent, CardFooter, CardHeader, CardTitle, CardDescription } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Separator } from '../components/ui/separator';
import { Alert, AlertDescription } from '../components/ui/alert';
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
import { useNavigate } from 'react-router-dom';
import { getUserAccounts, createAccount, Account, AccountCreationRequest } from '../api/accountApi';

const Accounts = () => {
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [isCreating, setIsCreating] = useState(false);
  const [newAccountName, setNewAccountName] = useState('');
  const [success, setSuccess] = useState('');
  const [dialogOpen, setDialogOpen] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    const fetchAccounts = async () => {
      try {
        setIsLoading(true);
        setError('');
        const data = await getUserAccounts();

        console.log('Accounts:', data);
        
        setAccounts(data);
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

  const handleCreateAccount = async () => {
    if (!newAccountName.trim()) {
      setError('Please enter an account name');
      return;
    }
    
    try {
      setIsCreating(true);
      setError('');
      setSuccess('');
      
      // Create the account request payload
      const accountRequest: AccountCreationRequest = { 
        name: newAccountName.trim() 
      };
      
      // Make API call to create the account
      console.log('Creating account with name:', accountRequest.name);
      const newAccount = await createAccount(accountRequest);
      console.log('Account created successfully:', newAccount);
      
      // Update the UI with the new account
      setSuccess(`Account "${newAccount.name}" was created successfully!`);
      setNewAccountName('');
      setDialogOpen(false);
      
      // Refresh accounts list
      const updatedAccounts = await getUserAccounts();
      setAccounts(updatedAccounts);
      
      // Clear success message after 5 seconds
      setTimeout(() => setSuccess(''), 5000);
    } catch (err) {
      console.error('Error creating account:', err);
      
      // Handle specific error cases
      if (err instanceof Error) {
        if (err.message.includes('Authentication required')) {
          setError('Authentication required. Please log in again.');
          navigate('/login');
        } else if (err.message.includes('Failed to create account')) {
          setError('Could not create account. Please try again later.');
        } else {
          setError(err.message);
        }
      } else {
        setError('An unexpected error occurred');
      }
    } finally {
      setIsCreating(false);
    }
  };

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
          <p className="text-muted-foreground">Manage your financial accounts</p>
        </div>
        <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
          <DialogTrigger asChild>
            <Button className="button-primary">Open New Account</Button>
          </DialogTrigger>
          <DialogContent className="bg-background border-2 shadow-lg">
            <DialogHeader>
              <DialogTitle className="text-primary">Create New Account</DialogTitle>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-4 items-center gap-4">
                <Label htmlFor="name" className="text-right text-foreground/90">
                  Account Name
                </Label>
                <Input
                  id="name"
                  value={newAccountName}
                  onChange={(e) => setNewAccountName(e.target.value)}
                  className="col-span-3 input-large"
                  placeholder="e.g. Savings, Checking, Emergency Fund"
                />
              </div>
            </div>
            <DialogFooter>
              <Button 
                onClick={handleCreateAccount} 
                disabled={isCreating || !newAccountName.trim()}
                className="button-primary"
              >
                {isCreating ? 'Creating...' : 'Create Account'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
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
            <p className="text-muted-foreground text-center max-w-md mb-6">Click the button below to open your first account.</p>
            <Button onClick={() => setDialogOpen(true)} className="button-primary">Open New Account</Button>
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-6">
          {accounts.map(account => (
            <Card key={account.id} className="card overflow-hidden">
              <CardHeader className="pb-2">
                <CardTitle className="text-xl text-primary">{account.name}</CardTitle>
                <CardDescription>Account #{account.id}</CardDescription>
              </CardHeader>
              <CardContent className="pt-4">
                <div className="flex flex-col md:flex-row justify-between md:items-start mb-6">
                  <div className="space-y-2">
                    <div className="text-sm text-muted-foreground">Available Balance</div>
                    <div className="text-3xl font-bold">{formatCurrency(account.amount)}</div>
                  </div>
                  <div className="mt-4 md:mt-0 self-start bg-secondary/30 px-4 py-2 rounded-md">
                    <div className="text-sm text-muted-foreground">User ID</div>
                    <div className="font-medium">{account.userId}</div>
                  </div>
                </div>
              </CardContent>
              
              <Separator className="bg-border/60" />
              
              <CardFooter className="flex flex-wrap gap-3 pt-6">
                <Button size="sm" onClick={() => navigate(`/accounts/${account.id}`)} className="button-primary h-10">
                  <svg className="mr-2 h-4 w-4" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path>
                    <circle cx="12" cy="12" r="3"></circle>
                  </svg>
                  View Details
                </Button>
                <Button variant="outline" size="sm" className="h-10">
                  <svg className="mr-2 h-4 w-4" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
                    <polyline points="14 2 14 8 20 8"></polyline>
                    <line x1="16" y1="13" x2="8" y2="13"></line>
                    <line x1="16" y1="17" x2="8" y2="17"></line>
                    <polyline points="10 9 9 9 8 9"></polyline>
                  </svg>
                  View Statements
                </Button>
                <Button variant="outline" size="sm" className="h-10">
                  <svg className="mr-2 h-4 w-4" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5L17 3z"></path>
                  </svg>
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