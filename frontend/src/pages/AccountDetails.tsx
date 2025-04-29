import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { ArrowLeft } from 'lucide-react';
import { getUserAccounts, Account, depositToAccount, DepositRequest } from '../api/accountApi';
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
    return <div className="text-center py-10">Loading account details...</div>;
  }

  if (error) {
    return (
      <div className="space-y-4">
        <Button variant="outline" onClick={() => navigate('/accounts')}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Back to Accounts
        </Button>
        <div className="text-red-500">{error}</div>
      </div>
    );
  }

  if (!account) {
    return (
      <div className="space-y-4">
        <Button variant="outline" onClick={() => navigate('/accounts')}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Back to Accounts
        </Button>
        <div>Account not found</div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="outline" onClick={() => navigate('/accounts')}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Back to Accounts
        </Button>
        <h1 className="text-2xl font-bold">Account Details</h1>
      </div>

      {depositSuccess && (
        <div className="bg-green-100 border border-green-400 text-green-700 px-4 py-3 rounded">
          {depositSuccess}
        </div>
      )}

      <Card>
        <CardHeader>
          <CardTitle>{account.name}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="grid gap-4">
            <div>
              <h3 className="text-sm font-medium text-muted-foreground">Account ID</h3>
              <p className="text-lg">{account.id}</p>
            </div>
            <div>
              <h3 className="text-sm font-medium text-muted-foreground">Balance</h3>
              <p className="text-2xl font-bold">{formatCurrency(account.amount)}</p>
            </div>
            <div>
              <h3 className="text-sm font-medium text-muted-foreground">User ID</h3>
              <p className="text-lg">{account.userId}</p>
            </div>
          </div>

          <div className="flex gap-4">
            <Dialog open={depositDialogOpen} onOpenChange={setDepositDialogOpen}>
              <DialogTrigger asChild>
                <Button>Deposit Money</Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>Deposit to {account.name}</DialogTitle>
                </DialogHeader>
                <div className="grid gap-4 py-4">
                  <div className="grid grid-cols-4 items-center gap-4">
                    <Label htmlFor="amount" className="text-right">
                      Amount
                    </Label>
                    <Input
                      id="amount"
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={depositAmount}
                      onChange={(e) => setDepositAmount(e.target.value)}
                      className="col-span-3"
                      placeholder="Enter amount to deposit"
                    />
                  </div>
                  {depositError && (
                    <div className="text-red-500 text-sm">{depositError}</div>
                  )}
                </div>
                <DialogFooter>
                  <Button 
                    onClick={handleDeposit} 
                    disabled={isDepositing || !depositAmount || parseFloat(depositAmount) <= 0}
                  >
                    {isDepositing ? 'Depositing...' : 'Deposit'}
                  </Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
            <Button variant="outline">View Statements</Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
};

export default AccountDetails; 