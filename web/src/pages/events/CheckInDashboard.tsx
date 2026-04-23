import { useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { AlertCircle, Users, Target } from 'lucide-react';
import { api, CheckInDashboardDto } from '../../lib/api';

export function CheckInDashboard() {
  const { id: eventId } = useParams<{ id: string }>();

  // Fetch dashboard data with automatic polling every 2 seconds
  const { data: dashboard, isLoading, error, isFetching } = useQuery({
    queryKey: ['check-in-dashboard', eventId],
    queryFn: () => {
      if (!eventId) throw new Error('Event ID is required');
      return api.getCheckInDashboard(eventId);
    },
    refetchInterval: 2000, // Poll every 2 seconds for real-time updates
    enabled: !!eventId,
    retry: true, // Auto-retry on network failure
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 10000), // Exponential backoff
  });

  // Check for network/connection errors
  const isOffline = error && (error as any).status === undefined;
  const isError = error && !isOffline;

  // Calculate progress percentage
  const progressPercent = dashboard
    ? Math.round((dashboard.totalCheckIns / dashboard.targetCount) * 100)
    : 0;

  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <h1 className="text-3xl font-bold">Live Check-In Dashboard</h1>
        <div className="animate-pulse space-y-4">
          <div className="h-32 bg-gray-200 rounded-lg" />
          <div className="h-24 bg-gray-200 rounded-lg" />
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">Live Check-In Dashboard</h1>
        {isFetching && (
          <div className="text-sm text-gray-600 flex items-center gap-2">
            <div className="animate-spin h-4 w-4 border-2 border-primary border-t-transparent rounded-full" />
            Updating...
          </div>
        )}
      </div>

      {/* Offline State */}
      {isOffline && (
        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4 flex items-start gap-3">
          <AlertCircle className="h-5 w-5 text-yellow-600 mt-0.5 flex-shrink-0" />
          <div>
            <h3 className="font-semibold text-yellow-900">No Connection</h3>
            <p className="text-sm text-yellow-800">
              The dashboard is offline. Retrying automatically when connection is restored...
            </p>
          </div>
        </div>
      )}

      {/* Error State */}
      {isError && !isOffline && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 flex items-start gap-3">
          <AlertCircle className="h-5 w-5 text-red-600 mt-0.5 flex-shrink-0" />
          <div>
            <h3 className="font-semibold text-red-900">Error</h3>
            <p className="text-sm text-red-800">
              {error instanceof Error ? error.message : 'Failed to load dashboard data'}
            </p>
          </div>
        </div>
      )}

      {/* Check-In Card */}
      {dashboard && (
        <div className="bg-white rounded-lg border border-gray-200 shadow-sm p-6 space-y-6">
          {/* Main Stats */}
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-gray-600">
                <Users className="h-5 w-5" />
                <span className="text-sm font-medium">Checked In</span>
              </div>
              <p className="text-4xl font-bold text-primary">
                {dashboard.totalCheckIns}
              </p>
            </div>
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-gray-600">
                <Target className="h-5 w-5" />
                <span className="text-sm font-medium">Target</span>
              </div>
              <p className="text-4xl font-bold text-gray-700">
                {dashboard.targetCount}
              </p>
            </div>
          </div>

          {/* Progress Bar */}
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-medium text-gray-700">Progress</h2>
              <span className="text-sm font-semibold text-gray-900">
                {progressPercent}%
              </span>
            </div>
            <div className="w-full bg-gray-200 rounded-full h-3 overflow-hidden">
              <div
                className="bg-primary h-full rounded-full transition-all duration-500 ease-out"
                style={{ width: `${progressPercent}%` }}
                role="progressbar"
                aria-valuenow={progressPercent}
                aria-valuemin={0}
                aria-valuemax={100}
              />
            </div>
            <p className="text-xs text-gray-600 text-right">
              {dashboard.totalCheckIns} of {dashboard.targetCount} participants
            </p>
          </div>
        </div>
      )}

      {/* Faction Breakdown */}
      {dashboard && dashboard.factionBreakdown.length > 0 && (
        <div className="bg-white rounded-lg border border-gray-200 shadow-sm p-6">
          <h2 className="text-lg font-bold mb-4">Faction Breakdown</h2>
          <div className="space-y-3">
            {dashboard.factionBreakdown.map((faction) => (
              <div key={faction.factionId} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                <span className="font-medium text-gray-900">{faction.factionName}</span>
                <span className="text-sm font-semibold text-gray-700">
                  {faction.checkInCount} checked in
                </span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
