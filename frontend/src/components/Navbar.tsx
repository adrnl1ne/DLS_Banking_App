import { Link } from 'react-router-dom';
import { Button } from './ui/button';
import { Avatar, AvatarFallback } from './ui/avatar';

interface NavbarProps {
  isAuthenticated: boolean;
  setIsAuthenticated: (value: boolean) => void;
}

const Navbar = ({ isAuthenticated, setIsAuthenticated }: NavbarProps) => {
  const handleLogout = () => {
    setIsAuthenticated(false);
  };

  return (
    <nav className="navbar shadow-lg">
      <div className="container mx-auto py-4 px-4 md:px-6 flex justify-between items-center">
        <Link to="/" className="text-xl font-bold tracking-tight">DLS Banking</Link>
        <div className="flex items-center gap-6">
          {isAuthenticated ? (
            <>
              <div className="hidden md:flex items-center gap-6">
                <Link to="/" className="navbar-link">Dashboard</Link>
                <Link to="/accounts" className="navbar-link">Accounts</Link>
                <Link to="/transactions" className="navbar-link">Transactions</Link>
              </div>
              <div className="flex items-center gap-3">
                <Avatar className="h-9 w-9 navbar-avatar">
                  <AvatarFallback className="text-sm font-medium">JD</AvatarFallback>
                </Avatar>
                <Button 
                  variant="outline" 
                  size="sm" 
                  onClick={handleLogout}
                  className="navbar-button"
                >
                  Logout
                </Button>
              </div>
            </>
          ) : (
            <>
              <div className="hidden md:flex items-center gap-6">
                <Link to="/login" className="navbar-link">Login</Link>
                <Link to="/register" className="navbar-link">Register</Link>
              </div>
              <Button asChild size="sm" variant="outline" className="navbar-button">
                <Link to="/register">Get Started</Link>
              </Button>
            </>
          )}
        </div>
      </div>
    </nav>
  );
};

export default Navbar; 