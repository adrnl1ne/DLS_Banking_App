import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { getUsers, createUser, User, UserRequest } from '../api/userApi';
import { createAccount, AccountCreationRequest } from '../api/accountApi';
import {
  Container,
  Typography,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Box,
  Alert,
  Tab,
  Tabs,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
} from '@mui/material';

interface TabPanelProps {
  children?: React.ReactNode;
  index: number;
  value: number;
}

function TabPanel(props: TabPanelProps) {
  const { children, value, index, ...other } = props;

  return (
    <div
      role="tabpanel"
      hidden={value !== index}
      id={`simple-tabpanel-${index}`}
      aria-labelledby={`simple-tab-${index}`}
      {...other}
    >
      {value === index && (
        <Box sx={{ p: 3 }}>
          {children}
        </Box>
      )}
    </div>
  );
}

const AdminPanel: React.FC = () => {
  const navigate = useNavigate();
  const [users, setUsers] = useState<User[]>([]);
  const [openUserDialog, setOpenUserDialog] = useState(false);
  const [openAccountDialog, setOpenAccountDialog] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [tabValue, setTabValue] = useState(0);
  const [userFormData, setUserFormData] = useState<UserRequest>({
    username: '',
    email: '',
    password: '',
    roleId: 1
  });
  const [accountFormData, setAccountFormData] = useState<AccountCreationRequest>({
    name: '',
    userId: 0
  });

  useEffect(() => {
    const fetchUsers = async () => {
      try {
        const users = await getUsers();
        setUsers(users);
      } catch (error) {
        console.error('Error fetching users:', error);
      }
    };
    fetchUsers();
  }, [navigate]);

  const handleCreateUser = async () => {
    try {
      const newUser = await createUser(userFormData);
      setUsers([...users, newUser]);
      setOpenUserDialog(false);
      setSuccess('User created successfully');
      // Reset form
      setUserFormData({
        username: '',
        email: '',
        password: '',
        roleId: 1
      });
    } catch (error) {
      console.error('Error creating user:', error);
      setError(error instanceof Error ? error.message : 'Failed to create user');
    }
  };

  const handleCreateAccount = async () => {
    try {
      if (accountFormData.userId === 0) {
        setError('Please select a user');
        return;
      }
      
      await createAccount(accountFormData);
      setOpenAccountDialog(false);
      setSuccess('Account created successfully');
      // Reset form
      setAccountFormData({
        name: '',
        userId: 0
      });
    } catch (error) {
      console.error('Error creating account:', error);
      setError(error instanceof Error ? error.message : 'Failed to create account');
    }
  };

  const handleTabChange = (_event: React.SyntheticEvent, newValue: number) => {
    setTabValue(newValue);
  };

  return (
    <Container maxWidth="lg" sx={{ mt: 4, mb: 4 }}>
      <Typography variant="h4" gutterBottom>
        Admin Panel
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {success && (
        <Alert severity="success" sx={{ mb: 2 }}>
          {success}
        </Alert>
      )}

      <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
        <Tabs value={tabValue} onChange={handleTabChange}>
          <Tab label="Users" />
          <Tab label="Accounts" />
        </Tabs>
      </Box>

      <TabPanel value={tabValue} index={0}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column' }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
            <Typography variant="h6">Users</Typography>
            <Button variant="contained" color="primary" onClick={() => setOpenUserDialog(true)}>
              Create User
            </Button>
          </Box>

          <TableContainer>
            <Table>
              <TableHead>
                <TableRow>
                  <TableCell>ID</TableCell>
                  <TableCell>Username</TableCell>
                  <TableCell>Email</TableCell>
                  <TableCell>Role</TableCell>
                  <TableCell>Created At</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {users.map((user) => (
                  <TableRow key={user.id}>
                    <TableCell>{user.id}</TableCell>
                    <TableCell>{user.username}</TableCell>
                    <TableCell>{user.email}</TableCell>
                    <TableCell>{user.role}</TableCell>
                    <TableCell>{new Date(user.createdAt).toLocaleDateString()}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      </TabPanel>

      <TabPanel value={tabValue} index={1}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column' }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
            <Typography variant="h6">Accounts</Typography>
            <Button variant="contained" color="primary" onClick={() => setOpenAccountDialog(true)}>
              Create Account
            </Button>
          </Box>

          <Typography variant="body1">
            Create and manage user accounts from this panel.
          </Typography>
        </Paper>
      </TabPanel>

      {/* Create User Dialog */}
      <Dialog open={openUserDialog} onClose={() => setOpenUserDialog(false)}>
        <DialogTitle>Create New User</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            margin="dense"
            label="Username"
            type="text"
            fullWidth
            value={userFormData.username}
            onChange={(e) => setUserFormData({ ...userFormData, username: e.target.value })}
          />
          <TextField
            margin="dense"
            label="Email"
            type="email"
            fullWidth
            value={userFormData.email}
            onChange={(e) => setUserFormData({ ...userFormData, email: e.target.value })}
          />
          <TextField
            margin="dense"
            label="Password"
            type="password"
            fullWidth
            value={userFormData.password}
            onChange={(e) => setUserFormData({ ...userFormData, password: e.target.value })}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenUserDialog(false)}>Cancel</Button>
          <Button onClick={handleCreateUser} variant="contained" color="primary">
            Create
          </Button>
        </DialogActions>
      </Dialog>

      {/* Create Account Dialog */}
      <Dialog open={openAccountDialog} onClose={() => setOpenAccountDialog(false)}>
        <DialogTitle>Create New Account</DialogTitle>
        <DialogContent>
          <FormControl fullWidth margin="dense">
            <InputLabel id="user-select-label">Select User</InputLabel>
            <Select
              labelId="user-select-label"
              value={accountFormData.userId}
              label="Select User"
              onChange={(e) => setAccountFormData({ ...accountFormData, userId: Number(e.target.value) })}
            >
              <MenuItem value={0}>Select a user</MenuItem>
              {users.map((user) => (
                <MenuItem key={user.id} value={user.id}>
                  {user.username} ({user.email})
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <TextField
            margin="dense"
            label="Account Name"
            type="text"
            fullWidth
            value={accountFormData.name}
            onChange={(e) => setAccountFormData({ ...accountFormData, name: e.target.value })}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenAccountDialog(false)}>Cancel</Button>
          <Button 
            onClick={handleCreateAccount} 
            variant="contained" 
            color="primary"
            disabled={accountFormData.userId === 0 || !accountFormData.name.trim()}
          >
            Create
          </Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
};

export default AdminPanel; 