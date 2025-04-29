import { useState, useEffect } from 'react';
import { Card, CardContent, CardFooter } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { 
  Dialog, 
  DialogContent, 
  DialogHeader, 
  DialogTitle, 
  DialogFooter,
  DialogTrigger 
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold">My Accounts</h1>
        <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
          <DialogTrigger asChild>
            <Button>Open New Account</Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Create New Account</DialogTitle>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-4 items-center gap-4">
                <Label htmlFor="name" className="text-right">
                  Account Name
                </Label>
                <Input
                  id="name"
                  value={newAccountName}
                  onChange={(e) => setNewAccountName(e.target.value)}
                  className="col-span-3"
                  placeholder="e.g. Savings, Checking, Emergency Fund"
                />
              </div>
            </div>
            <DialogFooter>
              <Button 
                onClick={handleCreateAccount} 
                disabled={isCreating || !newAccountName.trim()}
              >
                {isCreating ? 'Creating...' : 'Create Account'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>
      
      {success && (
        <Alert className="border-green-500 bg-green-50">
          <AlertDescription className="text-green-700">{success}</AlertDescription>
        </Alert>
      )}
      
      {error && (
        <Alert className="border-red-500 bg-red-50">
          <AlertDescription className="text-red-700">{error}</AlertDescription>
        </Alert>
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
                <Button size="sm" onClick={() => navigate(`/accounts/${account.id}`)}>
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