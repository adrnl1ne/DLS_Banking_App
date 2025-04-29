import { useState, FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Separator } from '../components/ui/separator';
import { Checkbox } from '../components/ui/checkbox';
import { login } from '../api/authApi';

interface LoginProps {
  setIsAuthenticated: (value: boolean) => void;
}

const Login = ({ setIsAuthenticated }: LoginProps) => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    
    // Validate inputs
    if (!email || !password) {
      setError('Please fill in all fields');
      return;
    }

    try {
      await login({ email, password });
      setIsAuthenticated(true);
    } catch (error) {
      setError(error instanceof Error ? error.message : 'Invalid email or password');
    }
  };

  return (
    <div className="flex items-center justify-center py-12">
      <Card className="auth-form w-full max-w-md shadow-lg">
        <CardHeader className="auth-form-header">
          <div className="flex justify-center mb-6">
            <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-primary/10">
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-8 h-8 text-primary">
                <path d="M6.5 7H4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-2.5" />
                <path d="M12 4V2" />
                <path d="M8 14v3" />
                <path d="M16 14v3" />
                <path d="M12 16v3" />
                <path d="M20 9H4" />
                <path d="M12 4C8.26 4 7.5 6 7.5 7C7.5 8 8.26 10 12 10C15.74 10 16.5 8 16.5 7C16.5 6 15.74 4 12 4z" />
              </svg>
            </div>
          </div>
          <CardTitle className="auth-form-title">Welcome to DLS Banking</CardTitle>
          <CardDescription className="text-center">Sign in to your account</CardDescription>
        </CardHeader>
        
        {error && (
          <div className="mx-6 mb-4 p-3 text-sm rounded-md error-message" role="alert">
            {error}
          </div>
        )}
        
        <CardContent className="pt-6">
          <form onSubmit={handleSubmit} className="space-y-5">
            <div className="space-y-2">
              <Label htmlFor="email" className="text-foreground/90">Email Address</Label>
              <Input
                type="email"
                id="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="Enter your email"
                className="input-large"
              />
            </div>
            
            <div className="space-y-2">
              <div className="flex justify-between items-center">
                <Label htmlFor="password" className="text-foreground/90">Password</Label>
                <Link to="#" className="text-sm hover:underline link-primary">
                  Forgot Password?
                </Link>
              </div>
              <Input
                type="password"
                id="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Enter your password"
                className="input-large"
              />
            </div>
            
            <div className="flex items-center space-x-2 py-2">
              <Checkbox 
                id="remember" 
                checked={rememberMe} 
                onCheckedChange={(checked) => setRememberMe(checked === true)}
              />
              <Label htmlFor="remember" className="text-sm font-normal cursor-pointer text-foreground/80">
                Remember me for 30 days
              </Label>
            </div>
            
            <Button type="submit" className="w-full button-primary mt-2 h-12 text-base">
              Sign In
            </Button>
          </form>
        </CardContent>
        
        <Separator className="my-6 bg-border/50" />
        
        <CardFooter className="flex justify-center pb-8 pt-2">
          <div className="text-center text-sm text-foreground/80">
            Don't have an account?{' '}
            <Link to="/register" className="font-medium hover:underline link-primary">
              Register here
            </Link>
          </div>
        </CardFooter>
      </Card>
    </div>
  );
};

export default Login;