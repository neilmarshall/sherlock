import { useEffect, useMemo, useState } from 'react';
import { ChevronsUpDown } from 'lucide-react';
import type { StoryMetadata } from '@/types/story';
import { fetchAllStoryMetadata } from '@/lib/api';
import { Button } from '@/components/ui/button';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';

interface StoryPickerProps {
  onSelect: (id: string) => void;
  disabled?: boolean;
}

export function StoryPicker({ onSelect, disabled }: StoryPickerProps) {
  const [open, setOpen] = useState(false);
  const [stories, setStories] = useState<StoryMetadata[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetchAllStoryMetadata()
      .then((list) => {
        if (!cancelled) setStories(list);
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Failed to load case list');
      });
    return () => {
      cancelled = true;
    };
  }, []);

  // Group by collection; sort groups by earliest year, items within by year then title.
  const grouped = useMemo(() => {
    if (!stories) return [];
    const groups = new Map<string, StoryMetadata[]>();
    for (const s of stories) {
      const list = groups.get(s.collection) ?? [];
      list.push(s);
      groups.set(s.collection, list);
    }
    return Array.from(groups.entries())
      .map(([collection, items]) => ({
        collection,
        items: [...items].sort(
          (a, b) => a.yearPublished - b.yearPublished || a.title.localeCompare(b.title),
        ),
        earliestYear: Math.min(...items.map((i) => i.yearPublished)),
      }))
      .sort((a, b) => a.earliestYear - b.earliestYear);
  }, [stories]);

  const handleSelect = (id: string) => {
    setOpen(false);
    onSelect(id);
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          role="combobox"
          aria-expanded={open}
          disabled={disabled || stories === null}
          className="font-['Cinzel',serif] text-primary bg-secondary border border-primary/40 hover:bg-primary hover:text-primary-foreground transition-colors"
        >
          Browse Cases
          <ChevronsUpDown className="ml-2 h-4 w-4 opacity-60" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[20rem] p-0" align="end">
        <Command
          filter={(value, search) => {
            const tokens = search.toLowerCase().trim().split(/\s+/).filter(Boolean);
            if (tokens.length === 0) return 1;
            const haystack = value.toLowerCase();
            return tokens.every((t) => haystack.includes(t)) ? 1 : 0;
          }}
        >
          <CommandInput placeholder="Search by title..." />
          <CommandList>
            <CommandEmpty>
              {error ? error : 'No matching cases.'}
            </CommandEmpty>
            {grouped.map(({ collection, items }) => (
              <CommandGroup
                key={collection}
                heading={collection}
                className="[&_[cmdk-group-heading]]:font-['Cinzel',serif] [&_[cmdk-group-heading]]:text-primary"
              >
                {items.map((s) => (
                  <CommandItem
                    key={s.id}
                    value={`${s.title} ${collection}`}
                    onSelect={() => handleSelect(s.id)}
                    className="font-['Lora',serif]"
                  >
                    <span className="flex-1 truncate">{s.title}</span>
                    <span className="ml-2 text-xs text-muted-foreground tabular-nums">
                      {s.yearPublished}
                    </span>
                  </CommandItem>
                ))}
              </CommandGroup>
            ))}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
