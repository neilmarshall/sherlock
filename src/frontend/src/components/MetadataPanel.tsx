import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet';
import { Separator } from '@/components/ui/separator';
import { Info } from 'lucide-react';
import type { Story } from '@/types/story';

interface MetadataPanelProps {
  story: Story;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

function MetadataField({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="py-3">
      <dt className="text-xs uppercase tracking-wider text-muted-foreground mb-1">{label}</dt>
      <dd className="text-foreground">{value}</dd>
    </div>
  );
}

export function MetadataPanel({ story, open, onOpenChange }: MetadataPanelProps) {
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetTrigger asChild>
        <button
          className="text-muted-foreground hover:text-primary transition-colors p-2"
          aria-label="Story details"
        >
          <Info size={20} />
        </button>
      </SheetTrigger>
      <SheetContent className="bg-card border-border">
        <SheetHeader>
          <SheetTitle className="font-['Cinzel',serif] text-primary">Case Details</SheetTitle>
        </SheetHeader>
        <dl className="mt-6">
          <MetadataField label="Title" value={story.title} />
          <Separator className="bg-border" />
          <MetadataField label="Collection" value={story.collection} />
          <Separator className="bg-border" />
          <MetadataField label="Year Published" value={story.yearPublished} />
          <Separator className="bg-border" />
          <MetadataField label="Word Count" value={story.wordCount.toLocaleString()} />
        </dl>
      </SheetContent>
    </Sheet>
  );
}
