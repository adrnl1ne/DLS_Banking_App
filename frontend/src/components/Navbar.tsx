import { Link, useNavigate } from 'react-router-dom';
import { Button } from './ui/button';
import { Avatar, AvatarFallback } from './ui/avatar';
import { logout, getCurrentUser } from '../api/authApi';

interface NavbarProps {
  isAuthenticated: boolean;
  setIsAuthenticated: (value: boolean) => void;
  isAdmin: boolean;
}

const Navbar = ({ isAuthenticated, setIsAuthenticated }: NavbarProps) => {
  const navigate = useNavigate();
  const user = getCurrentUser()?.user;
  
  // Get user initials for the avatar
  const getUserInitials = () => {
    if (!user) return 'U';
    
    const firstName = user.firstName || '';
    const lastName = user.lastName || '';
    
    if (firstName && lastName) {
      return `${firstName[0]}${lastName[0]}`.toUpperCase();
    } else if (firstName) {
      return firstName[0].toUpperCase();
    } else if (user.email) {
      return user.email[0].toUpperCase();
    }
    
    return 'U';
  };

  const handleLogout = () => {
    logout();
    setIsAuthenticated(false);
    navigate('/login');
  };

  return (
    <nav className="navbar shadow-lg">
      <div className="container mx-auto py-5 px-4 sm:px-6 lg:px-8 flex justify-between items-center">
        <Link to="/" className="text-2xl font-bold tracking-tight hover:opacity-90 transition-opacity">
          <span className="flex items-center gap-2">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="w-6 h-6">
              <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path>
              <polyline points="9 22 9 12 15 12 15 22"></polyline>
            </svg>
            Faktura Fabrikken
          </span>
        </Link>
        <div className="flex items-center gap-6">
          {isAuthenticated ? (
            <>
              <div className="hidden md:flex items-center gap-8">
                <Link to="/" className="navbar-link text-sm font-medium">Dashboard</Link>
                <Link to="/accounts" className="navbar-link text-sm font-medium">Accounts</Link>
                <Link to="/transactions" className="navbar-link text-sm font-medium">Transactions</Link>
                {user?.role === 'admin' && (
                  <Link to="/admin" className="navbar-link text-sm font-medium">Admin Panel</Link>
                )}
              </div>
              <div className="flex items-center gap-4">
                <Avatar className="h-10 w-10 navbar-avatar">
                  <AvatarFallback className="text-sm font-medium">{getUserInitials()}</AvatarFallback>
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
              <div className="hidden md:flex items-center gap-8">
                <Link to="/login" className="navbar-link text-sm font-medium">Login</Link>
                <Link to="/register" className="navbar-link text-sm font-medium">Register</Link>
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