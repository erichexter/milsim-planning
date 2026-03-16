import { useState } from 'react';
import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { GripVertical } from 'lucide-react';
import Markdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import type { InfoSection } from '@/lib/api';
import { SectionEditor } from './SectionEditor';
import { SectionAttachments } from './SectionAttachments';

interface SortableSectionCardProps {
  section: InfoSection;
  eventId: string;
  isCommander: boolean;
  onRefresh: () => void;
  onSave: (updates: { title: string; bodyMarkdown: string | null }) => Promise<void>;
  onDelete: () => Promise<void>;
}

export function SortableSectionCard({
  section,
  eventId,
  isCommander,
  onRefresh,
  onSave,
  onDelete,
}: SortableSectionCardProps) {
  const [expanded, setExpanded] = useState(false);
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, transform, transition } =
    useSortable({ id: section.id, disabled: !isCommander });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  return (
    <div ref={setNodeRef} style={style}>
      <Card>
        <CardContent className="space-y-3 p-4">
          <div className="flex items-center justify-between gap-2">
            <Button
              type="button"
              variant="ghost"
              className="h-auto p-0 text-left text-base font-semibold"
              onClick={() => setExpanded((value) => !value)}
            >
              {section.title}
            </Button>

            {isCommander && (
              <button
                type="button"
                aria-label="Drag section"
                ref={setActivatorNodeRef}
                {...listeners}
                {...attributes}
                className="cursor-grab touch-none rounded border p-2"
              >
                <GripVertical className="h-4 w-4" />
              </button>
            )}
          </div>

          {expanded && (
            <>
              {isCommander ? (
                <SectionEditor section={section} onSave={onSave} onDelete={onDelete} />
              ) : (
                <div className="prose prose-sm max-w-none rounded border p-3">
                  <Markdown remarkPlugins={[remarkGfm]}>
                    {section.bodyMarkdown || '*No content*'}
                  </Markdown>
                </div>
              )}

              <SectionAttachments
                eventId={eventId}
                sectionId={section.id}
                attachments={section.attachments}
                canUpload={isCommander}
                onRefresh={onRefresh}
              />
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
