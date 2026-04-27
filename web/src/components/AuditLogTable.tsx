import { useState } from 'react';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../components/ui/table';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { ArrowUpDown, FileJson } from 'lucide-react';
import { useAuditLog, type AuditLogQuery } from '../hooks/useAuditLog';
import { format } from 'date-fns';

interface AuditLogTableProps {
  eventId: string;
}

type SortBy = 'timestamp' | 'unitName' | 'actionType';
type SortOrder = 'asc' | 'desc';

export function AuditLogTable({ eventId }: AuditLogTableProps) {
  const [limit] = useState(20);
  const [offset, setOffset] = useState(0);
  const [unitNameFilter, setUnitNameFilter] = useState('');
  const [sortBy, setSortBy] = useState<SortBy>('timestamp');
  const [sortOrder, setSortOrder] = useState<SortOrder>('desc');

  const { data, isLoading, error } = useAuditLog(eventId, {
    limit,
    offset,
    unitName: unitNameFilter || undefined,
    sortBy,
    sortOrder,
  });

  const handleToggleSort = (field: SortBy) => {
    if (sortBy === field) {
      setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc');
    } else {
      setSortBy(field);
      setSortOrder('desc');
    }
  };

  const handleExportCsv = () => {
    if (!data?.entries.length) return;

    const headers = [
      'Timestamp',
      'Unit Name',
      'Unit Type',
      'Primary Frequency',
      'Alternate Frequency',
      'Action Type',
      'User',
      'Conflicting Unit',
    ];

    const rows = data.entries.map((entry) => [
      format(new Date(entry.timestamp), 'yyyy-MM-dd HH:mm:ss'),
      entry.unitName,
      entry.unitType,
      entry.primaryFrequency || '-',
      entry.alternateFrequency || '-',
      entry.actionType,
      entry.userName,
      entry.conflictingUnitName || '-',
    ]);

    const csv = [
      headers.join(','),
      ...rows.map((row) => row.map((cell) => `"${cell}"`).join(',')),
    ].join('\n');

    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `frequency-audit-log-${format(new Date(), 'yyyy-MM-dd')}.csv`;
    a.click();
    window.URL.revokeObjectURL(url);
  };

  if (error) {
    return (
      <div className="p-6 text-center text-red-600">
        Failed to load audit log. Please try again.
      </div>
    );
  }

  return (
    <div className="space-y-4 p-6">
      {/* Filters and Controls */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-1 gap-2">
          <Input
            type="text"
            placeholder="Filter by unit name..."
            value={unitNameFilter}
            onChange={(e) => {
              setUnitNameFilter(e.target.value);
              setOffset(0);
            }}
            className="max-w-xs"
          />
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={handleExportCsv}
          disabled={!data?.entries.length}
        >
          <FileJson className="h-4 w-4 mr-2" />
          Export CSV
        </Button>
      </div>

      {/* Table */}
      <div className="border rounded-lg overflow-x-auto">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead
                className="cursor-pointer hover:bg-muted"
                onClick={() => handleToggleSort('timestamp')}
              >
                <div className="flex items-center gap-2">
                  Timestamp
                  {sortBy === 'timestamp' && (
                    <ArrowUpDown className="h-4 w-4" />
                  )}
                </div>
              </TableHead>
              <TableHead
                className="cursor-pointer hover:bg-muted"
                onClick={() => handleToggleSort('unitName')}
              >
                <div className="flex items-center gap-2">
                  Unit / Type
                  {sortBy === 'unitName' && (
                    <ArrowUpDown className="h-4 w-4" />
                  )}
                </div>
              </TableHead>
              <TableHead>Primary Frequency</TableHead>
              <TableHead>Alternate Frequency</TableHead>
              <TableHead
                className="cursor-pointer hover:bg-muted"
                onClick={() => handleToggleSort('actionType')}
              >
                <div className="flex items-center gap-2">
                  Action
                  {sortBy === 'actionType' && (
                    <ArrowUpDown className="h-4 w-4" />
                  )}
                </div>
              </TableHead>
              <TableHead>User</TableHead>
              <TableHead>Conflicting Unit</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <TableRow>
                <TableCell colSpan={7} className="text-center py-8">
                  Loading audit log...
                </TableCell>
              </TableRow>
            ) : data?.entries.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                  No audit log entries found.
                </TableCell>
              </TableRow>
            ) : (
              data?.entries.map((entry) => (
                <TableRow key={entry.id}>
                  <TableCell className="text-sm">
                    {format(new Date(entry.timestamp), 'MMM dd, HH:mm:ss')}
                  </TableCell>
                  <TableCell>
                    <div className="text-sm">
                      <div className="font-medium">{entry.unitName}</div>
                      <div className="text-xs text-muted-foreground">{entry.unitType}</div>
                    </div>
                  </TableCell>
                  <TableCell className="text-sm font-mono">
                    {entry.primaryFrequency || '-'}
                  </TableCell>
                  <TableCell className="text-sm font-mono">
                    {entry.alternateFrequency || '-'}
                  </TableCell>
                  <TableCell className="text-sm">
                    <span className="inline-block px-2 py-1 rounded bg-muted text-xs font-medium">
                      {entry.actionType}
                    </span>
                  </TableCell>
                  <TableCell className="text-sm">{entry.userName}</TableCell>
                  <TableCell className="text-sm">
                    {entry.conflictingUnitName || '-'}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {/* Pagination Info */}
      {data && (
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <div>
            Showing {data.entries.length === 0 ? 0 : offset + 1} to{' '}
            {Math.min(offset + limit, data.total)} of {data.total} entries
          </div>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setOffset(Math.max(0, offset - limit))}
              disabled={offset === 0}
            >
              Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setOffset(offset + limit)}
              disabled={offset + limit >= data.total}
            >
              Next
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
