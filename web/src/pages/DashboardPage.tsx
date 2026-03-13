import { useAuth } from '../hooks/useAuth';

export function DashboardPage() {
  const { user, logout } = useAuth();
  return (
    <div>
      <h1>Dashboard</h1>
      <p>Welcome, {user?.callsign || user?.email}</p>
      <p>Role: {user?.role}</p>
      <button onClick={() => void logout()}>Logout</button>
    </div>
  );
}
