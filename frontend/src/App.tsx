import { BrowserRouter as Router, Routes, Route } from 'react-router-dom'
import { useState } from 'react'
import Navbar from './components/Navbar'
import Dashboard from './pages/Dashboard'
import Accounts from './pages/Accounts'
import Transactions from './pages/Transactions'
import Login from './pages/Login'
import Register from './pages/Register'
import './App.css'

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(false)

  return (
    <Router>
      <div className="min-h-screen flex flex-col bg-background text-foreground">
        <Navbar isAuthenticated={isAuthenticated} setIsAuthenticated={setIsAuthenticated} />
        <main className="flex-1 flex justify-center py-8">
          <div className="container mx-auto px-4 md:px-6 max-w-7xl">
            <Routes>
              <Route path="/" element={isAuthenticated ? <Dashboard /> : <Login setIsAuthenticated={setIsAuthenticated} />} />
              <Route path="/accounts" element={isAuthenticated ? <Accounts /> : <Login setIsAuthenticated={setIsAuthenticated} />} />
              <Route path="/transactions" element={isAuthenticated ? <Transactions /> : <Login setIsAuthenticated={setIsAuthenticated} />} />
              <Route path="/login" element={<Login setIsAuthenticated={setIsAuthenticated} />} />
              <Route path="/register" element={<Register />} />
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
