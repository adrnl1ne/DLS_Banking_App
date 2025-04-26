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
      <Card className="w-full max-w-md shadow-lg" style={{ borderColor: 'var(--border)' }}>
        <CardHeader className="space-y-2 pb-6 card-header-light">
          <CardTitle className="text-2xl font-bold text-center card-title-primary">DLS Banking</CardTitle>
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
              <Label htmlFor="email">Email Address</Label>
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
                <Label htmlFor="password">Password</Label>
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
            
            <div className="flex items-center space-x-2">
              <Checkbox 
                id="remember" 
                checked={rememberMe} 
                onCheckedChange={(checked) => setRememberMe(checked === true)}
              />
              <Label htmlFor="remember" className="text-sm font-normal cursor-pointer">
                Remember me for 30 days
              </Label>
            </div>
            
            <Button type="submit" className="w-full button-primary">
              Sign In
            </Button>
          </form>
        </CardContent>
        
        <Separator className="my-4" />
        
        <CardFooter className="flex justify-center pb-8 pt-4">
          <div className="text-center text-sm">
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