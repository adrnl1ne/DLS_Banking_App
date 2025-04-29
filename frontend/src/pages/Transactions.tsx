import { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '../components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';
import { Label } from '../components/ui/label';
import { getAccountTransactions, Transaction as ApiTransaction } from '../api/transactionApi';
import { getUserAccounts, Account } from '../api/accountApi';
import { format } from 'date-fns';

interface TransactionWithAccountName extends ApiTransaction {
  accountName: string;
  type: 'credit' | 'debit';
}

const Transactions = () => {
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [transactions, setTransactions] = useState<TransactionWithAccountName[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filterAccount, setFilterAccount] = useState<string>('all');
  const [filterType, setFilterType] = useState<string>('all');
  const [backendError, setBackendError] = useState<boolean>(false);

  useEffect(() => {
    const fetchData = async () => {
      try {
        setLoading(true);
        setBackendError(false);
        
        // Fetch user accounts
        const userAccounts = await getUserAccounts();
        setAccounts(userAccounts);
        
        // Fetch transactions for each account
        const allTransactions: TransactionWithAccountName[] = [];
        let anyRequestFailed = false;
        
        for (const account of userAccounts) {
          try {
            const accountTransactions = await getAccountTransactions(account.id.toString());
            
            // If transactions array is empty, it might be due to a backend error (handled in the API)
            if (accountTransactions.length === 0) {
              continue;
            }
            
            // Enhance transactions with account name and type
            const enhancedTransactions = accountTransactions.map(transaction => {
              const type: 'credit' | 'debit' = transaction.toAccount === account.id.toString() ? 'credit' : 'debit';
              return {
                ...transaction,
                accountName: account.name,
                type
              };
            });
            
            allTransactions.push(...enhancedTransactions);
          } catch (err) {
            anyRequestFailed = true;
            console.error(`Error fetching transactions for account ${account.id}:`, err);
          }
        }

        // If no transactions were fetched and at least one request failed, 
        // the backend might be having issues
        if (allTransactions.length === 0 && anyRequestFailed) {
          setBackendError(true);
        }
        
        // Sort by date descending
        allTransactions.sort((a, b) => 
          new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
        );
        
        setTransactions(allTransactions);
        setLoading(false);
      } catch (err) {
        console.error('Error fetching data:', err);
        setError('Failed to load transactions. Please try again later.');
        setLoading(false);
      }
    };

    fetchData();
  }, []);

  const filteredTransactions = transactions.filter(transaction => {
    if (filterAccount !== 'all' && transaction.accountName !== filterAccount) {
      return false;
    }
    if (filterType !== 'all' && transaction.type !== filterType) {
      return false;
    }
    return true;
  });

  if (loading) {
    return (
      <div className="py-16 flex justify-center items-center">
        <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-primary"></div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="text-center py-10">
        <div className="bg-red-50 border border-red-200 rounded-md p-4">
          <p className="text-red-500 font-medium">{error}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <div className="flex flex-col space-y-2 mb-4">
        <h1 className="text-2xl font-bold text-primary">Transaction History</h1>
        <p className="text-muted-foreground">View and filter your account transactions</p>
      </div>
      
      <Card className="card">
        <CardHeader className="pb-2">
          <CardTitle className="text-xl text-primary">Filters</CardTitle>
          <CardDescription>Customize your transaction view</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div className="space-y-2">
              <Label htmlFor="account" className="text-foreground/90">Account</Label>
              <select 
                id="account"
                className="w-full p-2 border border-input rounded-md bg-background transition-colors focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
                value={filterAccount}
                onChange={(e) => setFilterAccount(e.target.value)}
              >
                <option value="all">All Accounts</option>
                {accounts.map(account => (
                  <option key={account.id} value={account.name}>
                    {account.name}
                  </option>
                ))}
              </select>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="type" className="text-foreground/90">Transaction Type</Label>
              <select 
                id="type"
                className="w-full p-2 border border-input rounded-md bg-background transition-colors focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
                value={filterType}
                onChange={(e) => setFilterType(e.target.value)}
              >
                <option value="all">All Transactions</option>
                <option value="credit">Deposits</option>
                <option value="debit">Withdrawals</option>
              </select>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="date-range" className="text-foreground/90">Date Range</Label>
              <select 
                id="date-range"
                className="w-full p-2 border border-input rounded-md bg-background transition-colors focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
              >
                <option>Last 30 days</option>
                <option>Last 90 days</option>
                <option>Year to date</option>
                <option>Custom range</option>
              </select>
            </div>
          </div>
        </CardContent>
      </Card>
      
      {backendError ? (
        <Card className="card">
          <CardContent className="flex flex-col items-center justify-center py-10">
            <div className="bg-secondary/40 inline-flex items-center justify-center w-16 h-16 rounded-full mb-4">
              <svg className="h-8 w-8 text-muted-foreground" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="10"></circle>
                <line x1="12" y1="8" x2="12" y2="12"></line>
                <line x1="12" y1="16" x2="12.01" y2="16"></line>
              </svg>
            </div>
            <div className="text-primary font-semibold text-xl mb-2">Transaction Data Temporarily Unavailable</div>
            <p className="text-center text-muted-foreground max-w-md">
              We're currently experiencing technical difficulties with the transaction service. 
              Our team is working on resolving this issue. Please try again later.
            </p>
          </CardContent>
        </Card>
      ) : filteredTransactions.length === 0 ? (
        <Card className="card">
          <CardContent className="flex flex-col items-center justify-center py-16">
            <div className="bg-secondary/40 inline-flex items-center justify-center w-16 h-16 rounded-full mb-4">
              <svg className="h-8 w-8 text-muted-foreground" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M21 9V6a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-3"></path>
                <path d="M16 16l5-5-5-5"></path>
                <path d="M8 12h13"></path>
              </svg>
            </div>
            <p className="text-lg font-medium mb-2">No transactions found</p>
            <p className="text-muted-foreground text-center max-w-md">Try changing your filters or make a transaction to see it here.</p>
          </CardContent>
        </Card>
      ) : (
        <Card className="card overflow-hidden">
          <CardHeader className="pb-2">
            <CardTitle className="text-xl text-primary">Transaction Results</CardTitle>
            <CardDescription>{filteredTransactions.length} transactions found</CardDescription>
          </CardHeader>
          <CardContent className="p-0">
            <div className="rounded-md border overflow-hidden">
              <Table>
                <TableHeader className="bg-secondary/50">
                  <TableRow>
                    <TableHead className="font-medium">Date</TableHead>
                    <TableHead className="font-medium">Description</TableHead>
                    <TableHead className="font-medium">Status</TableHead>
                    <TableHead className="font-medium">Account</TableHead>
                    <TableHead className="font-medium text-right">Amount</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {filteredTransactions.map(transaction => (
                    <TableRow key={transaction.transferId} className="hover:bg-secondary/10 transition-colors">
                      <TableCell className="font-medium">{format(new Date(transaction.createdAt), 'yyyy-MM-dd')}</TableCell>
                      <TableCell>
                        {transaction.type === 'credit' 
                          ? `Transfer from ${transaction.fromAccount}` 
                          : `Transfer to ${transaction.toAccount}`}
                      </TableCell>
                      <TableCell>
                        <span className={`inline-block px-2 py-1 text-xs rounded-full ${
                          transaction.status === 'completed' 
                            ? 'bg-green-50 text-green-700 border border-green-200' 
                            : transaction.status === 'pending'
                            ? 'bg-yellow-50 text-yellow-700 border border-yellow-200'
                            : 'bg-red-50 text-red-700 border border-red-200'
                        }`}>
                          {transaction.status}
                        </span>
                      </TableCell>
                      <TableCell>{transaction.accountName}</TableCell>
                      <TableCell className={`text-right font-medium ${
                        transaction.type === 'credit' ? 'amount-credit' : 'amount-debit'
                      }`}>
                        {transaction.type === 'credit' ? '+' : '-'}${transaction.amount.toFixed(2)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
};

export default Transactions; 