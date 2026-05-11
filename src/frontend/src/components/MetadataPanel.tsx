import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet';
import { Separator } from '@/components/ui/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Info } from 'lucide-react';
import type { Dispatch, SetStateAction } from 'react';
import type { Story } from '@/types/story';
import type { ChatMessage } from '@/types/chat';
import { ChatTab } from '@/components/ChatTab';

type PanelTab = 'info' | 'chat';

interface MetadataPanelProps {
  story: Story;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  chatMessages: ChatMessage[];
  onChatMessagesChange: Dispatch<SetStateAction<ChatMessage[]>>;
  activeTab: PanelTab;
  onActiveTabChange: (tab: PanelTab) => void;
}

function MetadataField({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="py-3">
      <dt className="text-xs uppercase tracking-wider text-muted-foreground mb-1">{label}</dt>
      <dd className="text-foreground">{value}</dd>
    </div>
  );
}

export function MetadataPanel({
  story,
  open,
  onOpenChange,
  chatMessages,
  onChatMessagesChange,
  activeTab,
  onActiveTabChange,
}: MetadataPanelProps) {
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
        <Tabs
          value={activeTab}
          onValueChange={(value) => onActiveTabChange(value as PanelTab)}
          className="mt-4 px-4 flex-1 min-h-0"
        >
          <TabsList className="w-full">
            <TabsTrigger value="info">Info</TabsTrigger>
            <TabsTrigger value="chat">Chat</TabsTrigger>
          </TabsList>
          <TabsContent value="info">
            <dl className="mt-4">
              <MetadataField label="Title" value={story.title} />
              <Separator className="bg-border" />
              <MetadataField label="Collection" value={story.collection} />
              <Separator className="bg-border" />
              <MetadataField label="Year Published" value={story.yearPublished} />
              <Separator className="bg-border" />
              <MetadataField label="Word Count" value={story.wordCount.toLocaleString()} />
            </dl>
          </TabsContent>
          <TabsContent
            value="chat"
            forceMount
            className="mt-4 flex flex-col min-h-0 data-[state=inactive]:hidden"
          >
            <ChatTab
              key={story.id}
              storyId={story.id}
              messages={chatMessages}
              onMessagesChange={onChatMessagesChange}
            />
          </TabsContent>
        </Tabs>
      </SheetContent>
    </Sheet>
  );
}
