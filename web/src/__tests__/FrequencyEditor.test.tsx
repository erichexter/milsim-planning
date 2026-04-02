import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FrequencyEditor } from '../components/frequency/FrequencyEditor';

describe('FrequencyEditor', () => {
  const defaultProps = {
    label: 'Alpha Squad',
    initialPrimary: '145.500',
    initialBackup: '145.600',
    onSave: vi.fn(),
    isPending: false,
    error: null,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders primary and backup inputs pre-filled with current values', () => {
    render(<FrequencyEditor {...defaultProps} />);

    const primaryInput = screen.getByTestId('primary-input') as HTMLInputElement;
    const backupInput = screen.getByTestId('backup-input') as HTMLInputElement;

    expect(primaryInput.value).toBe('145.500');
    expect(backupInput.value).toBe('145.600');
  });

  it('submits correct body shape on form submit', async () => {
    const onSave = vi.fn();
    const user = userEvent.setup();

    render(<FrequencyEditor {...defaultProps} onSave={onSave} />);

    const primaryInput = screen.getByTestId('primary-input');
    await user.clear(primaryInput);
    await user.type(primaryInput, '146.000');

    const saveButton = screen.getByRole('button', { name: /save/i });
    await user.click(saveButton);

    expect(onSave).toHaveBeenCalledWith({
      primary: '146.000',
      backup: '145.600',
    });
  });

  it('invalidates query cache on success via onSave callback', async () => {
    const onSave = vi.fn();
    const user = userEvent.setup();

    render(<FrequencyEditor {...defaultProps} onSave={onSave} />);

    const saveButton = screen.getByRole('button', { name: /save/i });
    await user.click(saveButton);

    expect(onSave).toHaveBeenCalledTimes(1);
    expect(onSave).toHaveBeenCalledWith({
      primary: '145.500',
      backup: '145.600',
    });
  });

  it('shows error message on mutation failure', () => {
    const error = new Error('Network error');

    render(<FrequencyEditor {...defaultProps} error={error} />);

    expect(screen.getByTestId('editor-error')).toBeInTheDocument();
    expect(screen.getByText('Network error')).toBeInTheDocument();
  });

  it('disables submit button while mutation is pending', () => {
    render(<FrequencyEditor {...defaultProps} isPending={true} />);

    const saveButton = screen.getByRole('button', { name: /saving/i });
    expect(saveButton).toBeDisabled();
  });
});
