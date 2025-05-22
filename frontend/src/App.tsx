import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom'
import { useState, useEffect } from 'react'
import Navbar from './components/Navbar'
import Dashboard from './pages/Dashboard'
import Accounts from './pages/Accounts'
import AccountDetails from './pages/AccountDetails'
import Transactions from './pages/Transactions'
import Login from './pages/Login'
import Register from './pages/Register'
import AdminPanel from './pages/AdminPanel'
import { isAuthenticated, getCurrentUser } from './api/authApi'
import './App.css'

function App() {
  const [isLoggedIn, setIsLoggedIn] = useState(false)
  const [isAdmin, setIsAdmin] = useState(false)

  // Check authentication status on app load
  useEffect(() => {
    const authStatus = isAuthenticated();
    setIsLoggedIn(authStatus);
    
    if (authStatus) {
      const currentUser = getCurrentUser();
      console.log(currentUser?.user.role === 'admin')
      setIsAdmin(currentUser?.user.role === 'admin');
    }
  }, []);

  console.log(isAdmin)

  return (
    <Router>
      <div className="min-h-screen flex flex-col bg-background text-foreground">
        <Navbar isAuthenticated={isLoggedIn} setIsAuthenticated={setIsLoggedIn} isAdmin={isAdmin} />
        <main className="flex-1 flex justify-center py-10">
          <div className="container mx-auto px-4 sm:px-6 lg:px-8 max-w-6xl">
            <Routes>
              <Route path="/" element={isLoggedIn ? <Dashboard /> : <Login setIsAuthenticated={setIsLoggedIn} />} />
              <Route path="/accounts" element={isLoggedIn ? <Accounts /> : <Navigate to="/login" />} />
              <Route path="/accounts/:accountId" element={isLoggedIn ? <AccountDetails /> : <Navigate to="/login" />} />
              <Route path="/transactions" element={isLoggedIn ? <Transactions /> : <Navigate to="/login" />} />
              <Route path="/login" element={isLoggedIn ? <Navigate to="/" /> : <Login setIsAuthenticated={setIsLoggedIn} />} />
              <Route path="/register" element={isLoggedIn ? <Navigate to="/" /> : <Register />} />
              <Route path="/admin" element={isLoggedIn && isAdmin ? <AdminPanel /> : <Navigate to="/" />} />
            </Routes>
          </div>
        </main>
        <footer className="py-8 mt-auto border-t border-border bg-secondary">
          <div className="container mx-auto px-4 sm:px-6 lg:px-8 text-center">
            <p className="text-sm text-muted-foreground">© 2025 Faktura Fabrikken. All rights reserved.</p>
          </div>
        </footer>
      </div>
    </Router>
  )
}

export default App
