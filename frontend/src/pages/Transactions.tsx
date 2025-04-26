import { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
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
    return <div className="text-center py-10">Loading transactions...</div>;
  }

  if (error) {
    return <div className="text-center py-10 text-red-500">{error}</div>;
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Transaction History</h1>
      
      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div className="space-y-2">
              <Label htmlFor="account">Account</Label>
              <select 
                id="account"
                className="w-full p-2 border border-input rounded-md"
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
              <Label htmlFor="type">Transaction Type</Label>
              <select 
                id="type"
                className="w-full p-2 border border-input rounded-md"
                value={filterType}
                onChange={(e) => setFilterType(e.target.value)}
              >
                <option value="all">All Transactions</option>
                <option value="credit">Deposits</option>
                <option value="debit">Withdrawals</option>
              </select>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="date-range">Date Range</Label>
              <select 
                id="date-range"
                className="w-full p-2 border border-input rounded-md"
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
        <Card className="p-8 text-center">
          <div className="text-amber-600 text-xl mb-4">Transaction Data Temporarily Unavailable</div>
          <p className="text-gray-600">
            We're currently experiencing technical difficulties with the transaction service. 
            Our team is working on resolving this issue. Please try again later.
          </p>
        </Card>
      ) : filteredTransactions.length === 0 ? (
        <div className="text-center py-10">No transactions found.</div>
      ) : (
        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Date</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Account</TableHead>
                  <TableHead className="text-right">Amount</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredTransactions.map(transaction => (
                  <TableRow key={transaction.transferId}>
                    <TableCell>{format(new Date(transaction.createdAt), 'yyyy-MM-dd')}</TableCell>
                    <TableCell>
                      {transaction.type === 'credit' 
                        ? `Transfer from ${transaction.fromAccount}` 
                        : `Transfer to ${transaction.toAccount}`}
                    </TableCell>
                    <TableCell>
                      <span className={`inline-block px-2 py-1 text-xs rounded-full ${
                        transaction.status === 'completed' 
                          ? 'bg-green-100 text-green-800' 
                          : transaction.status === 'pending'
                          ? 'bg-yellow-100 text-yellow-800'
                          : 'bg-red-100 text-red-800'
                      }`}>
                        {transaction.status}
                      </span>
                    </TableCell>
                    <TableCell>{transaction.accountName}</TableCell>
                    <TableCell className={`text-right ${
                      transaction.type === 'credit' ? 'text-green-600' : 'text-red-600'
                    }`}>
                      {transaction.type === 'credit' ? '+' : '-'}${transaction.amount.toFixed(2)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </div>
  );
};

export default Transactions; 