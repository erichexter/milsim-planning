import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SectionEditor } from '../components/content/SectionEditor';

const section = {
  id: 'sec-1',
  title: 'Initial title',
  bodyMarkdown: '**Bold** text',
  order: 0,
  attachments: [],
};

describe('SectionEditor', () => {
  it('renders Edit and Preview tabs', () => {
    render(
      <SectionEditor
        section={section}
        onSave={async () => {}}
        onDelete={async () => {}}
      />
    );

    expect(screen.getByRole('tab', { name: 'Edit' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Preview' })).toBeInTheDocument();
  });

  it('disables Save when title is empty and enables with non-empty title', async () => {
    const user = userEvent.setup();
    render(
      <SectionEditor
        section={section}
        onSave={async () => {}}
        onDelete={async () => {}}
      />
    );

    const titleInput = screen.getByPlaceholderText('Section title');
    const saveButton = screen.getByRole('button', { name: 'Save' });

    await user.clear(titleInput);
    expect(saveButton).toBeDisabled();

    await user.type(titleInput, 'Updated');
    expect(saveButton).toBeEnabled();
  });

  it('switches to Preview tab and renders markdown output', async () => {
    const user = userEvent.setup();
    const onSave = vi.fn(async () => {});
    render(
      <SectionEditor
        section={section}
        onSave={onSave}
        onDelete={async () => {}}
      />
    );

    await user.click(screen.getByRole('tab', { name: 'Preview' }));
    expect(screen.getByText('Bold')).toBeInTheDocument();
    expect(onSave).not.toHaveBeenCalled();
  });
});
