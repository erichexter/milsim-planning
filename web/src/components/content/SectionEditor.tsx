import { useEffect, useState } from 'react';
import Markdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { Button } from '@/components/ui/button';
import type { InfoSection } from '@/lib/api';

interface SectionEditorProps {
  section: InfoSection;
  onSave: (updates: { title: string; bodyMarkdown: string | null }) => Promise<void>;
  onDelete: () => Promise<void>;
}

export function SectionEditor({ section, onSave, onDelete }: SectionEditorProps) {
  const [title, setTitle] = useState(section.title);
  const [body, setBody] = useState(section.bodyMarkdown ?? '');
  const [saving, setSaving] = useState(false);
  const [activeTab, setActiveTab] = useState<'edit' | 'preview'>('edit');

  useEffect(() => {
    setTitle(section.title);
    setBody(section.bodyMarkdown ?? '');
  }, [section.bodyMarkdown, section.title]);

  const canSave = title.trim().length > 0;

  const handleSave = async () => {
    if (!canSave || saving) return;
    setSaving(true);
    try {
      await onSave({ title: title.trim(), bodyMarkdown: body.trim() ? body : null });
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!window.confirm('Delete this section?')) return;
    await onDelete();
  };

  return (
    <div className="space-y-3">
      <input
        className="w-full rounded border px-3 py-2"
        placeholder="Section title"
        value={title}
        onChange={(e) => setTitle(e.target.value)}
      />

      <div className="space-y-3">
        <div role="tablist" aria-label="Editor tabs" className="flex gap-2 border-b pb-2">
          <button
            role="tab"
            aria-selected={activeTab === 'edit'}
            className={`rounded px-3 py-1 text-sm ${activeTab === 'edit' ? 'bg-gray-200' : ''}`}
            onClick={() => setActiveTab('edit')}
            type="button"
          >
            Edit
          </button>
          <button
            role="tab"
            aria-selected={activeTab === 'preview'}
            className={`rounded px-3 py-1 text-sm ${activeTab === 'preview' ? 'bg-gray-200' : ''}`}
            onClick={() => setActiveTab('preview')}
            type="button"
          >
            Preview
          </button>
        </div>

        {activeTab === 'edit' ? (
          <textarea
            className="min-h-[200px] w-full rounded border p-3 font-mono"
            value={body}
            onChange={(e) => setBody(e.target.value)}
            placeholder="Write briefing markdown..."
          />
        ) : (
          <div className="prose max-w-none rounded border p-3">
            <Markdown remarkPlugins={[remarkGfm]}>{body || '*No content yet*'}</Markdown>
          </div>
        )}
      </div>

      <div className="flex gap-2">
        <Button type="button" onClick={handleSave} disabled={!canSave || saving}>
          {saving ? 'Saving...' : 'Save'}
        </Button>
        <Button type="button" variant="secondary" onClick={handleDelete}>
          Delete
        </Button>
      </div>
    </div>
  );
}
