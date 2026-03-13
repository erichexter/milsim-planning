import { useEffect, useState } from 'react';
import {
  DndContext,
  PointerSensor,
  KeyboardSensor,
  closestCenter,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import {
  SortableContext,
  arrayMove,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { api, type InfoSection } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { SortableSectionCard } from './SortableSectionCard';
import { SectionEditor } from './SectionEditor';

interface SectionListProps {
  eventId: string;
  sections: InfoSection[];
  onRefresh: () => void;
  isCommander: boolean;
}

const blankSection: InfoSection = {
  id: 'new',
  title: '',
  bodyMarkdown: '',
  order: 0,
  attachments: [],
};

export function SectionList({ eventId, sections, onRefresh, isCommander }: SectionListProps) {
  const [items, setItems] = useState<InfoSection[]>(sections);
  const [adding, setAdding] = useState(false);

  useEffect(() => {
    setItems(sections.slice().sort((a, b) => a.order - b.order));
  }, [sections]);

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    })
  );

  const handleDragEnd = async (event: DragEndEvent) => {
    const { active, over } = event;
    if (!isCommander || !over || active.id === over.id) return;

    const oldIndex = items.findIndex((item) => item.id === active.id);
    const newIndex = items.findIndex((item) => item.id === over.id);
    if (oldIndex < 0 || newIndex < 0) return;

    const reordered = arrayMove(items, oldIndex, newIndex).map((item, index) => ({
      ...item,
      order: index,
    }));

    setItems(reordered);
    await api.reorderInfoSections(
      eventId,
      reordered.map((item) => item.id)
    );
    onRefresh();
  };

  const saveNewSection = async (updates: { title: string; bodyMarkdown: string | null }) => {
    await api.createInfoSection(eventId, {
      title: updates.title,
      bodyMarkdown: updates.bodyMarkdown,
      order: items.length,
    });
    setAdding(false);
    onRefresh();
  };

  return (
    <div className="space-y-4">
      <DndContext
        collisionDetection={closestCenter}
        sensors={sensors}
        onDragEnd={handleDragEnd}
      >
        <SortableContext
          items={items.map((item) => item.id)}
          strategy={verticalListSortingStrategy}
        >
          <div className="space-y-3">
            {items.map((section) => (
              <SortableSectionCard
                key={section.id}
                section={section}
                eventId={eventId}
                isCommander={isCommander}
                onRefresh={onRefresh}
                onSave={(updates) =>
                  api
                    .updateInfoSection(eventId, section.id, {
                      title: updates.title,
                      bodyMarkdown: updates.bodyMarkdown,
                      order: section.order,
                    })
                    .then(onRefresh)
                }
                onDelete={() => api.deleteInfoSection(eventId, section.id).then(onRefresh)}
              />
            ))}
          </div>
        </SortableContext>
      </DndContext>

      {isCommander && (
        <div className="space-y-3">
          {!adding ? (
            <Button type="button" variant="outline" onClick={() => setAdding(true)}>
              Add Section
            </Button>
          ) : (
            <div className="rounded border p-4">
              <SectionEditor
                section={blankSection}
                onSave={saveNewSection}
                onDelete={async () => setAdding(false)}
              />
            </div>
          )}
        </div>
      )}
    </div>
  );
}
