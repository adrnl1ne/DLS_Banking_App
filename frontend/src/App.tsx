import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom'
import { useState, useEffect } from 'react'
import Navbar from './components/Navbar'
import Dashboard from './pages/Dashboard'
import Accounts from './pages/Accounts'
import Transactions from './pages/Transactions'
import Login from './pages/Login'
import Register from './pages/Register'
import { isAuthenticated } from './api/authApi'
import './App.css'

function App() {
  const [isLoggedIn, setIsLoggedIn] = useState(false)

  // Check authentication status on app load
  useEffect(() => {
    const authStatus = isAuthenticated();
    setIsLoggedIn(authStatus);
  }, []);

  return (
    <Router>
      <div className="min-h-screen flex flex-col bg-background text-foreground">
        <Navbar isAuthenticated={isLoggedIn} setIsAuthenticated={setIsLoggedIn} />
        <main className="flex-1 flex justify-center py-8">
          <div className="container mx-auto px-4 md:px-6 max-w-7xl">
            <Routes>
              <Route path="/" element={isLoggedIn ? <Dashboard /> : <Login setIsAuthenticated={setIsLoggedIn} />} />
              <Route path="/accounts" element={isLoggedIn ? <Accounts /> : <Navigate to="/login" />} />
              <Route path="/transactions" element={isLoggedIn ? <Transactions /> : <Navigate to="/login" />} />
              <Route path="/login" element={isLoggedIn ? <Navigate to="/" /> : <Login setIsAuthenticated={setIsLoggedIn} />} />
              <Route path="/register" element={isLoggedIn ? <Navigate to="/" /> : <Register />} />
            </Routes>
          </div>
        </main>
        <footer className="py-6 mt-8 border-t border-border bg-muted">
          <div className="container mx-auto px-4 md:px-6 text-center text-sm text-muted-foreground">
            <p>© 2025 DLS Banking. All rights reserved.</p>
          </div>
        </footer>
      </div>
    </Router>
  )
}

export default App
