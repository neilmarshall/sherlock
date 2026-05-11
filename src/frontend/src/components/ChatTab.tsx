import { useEffect, useRef, useState } from 'react';
import { AlertCircle, Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ScrollArea } from '@/components/ui/scroll-area';
import { streamChatReply } from '@/lib/api';
import type { ChatMessage } from '@/types/chat';
import { cn } from '@/lib/utils';

interface ChatTabProps {
  storyId: string;
}

export function ChatTab({ storyId }: ChatTabProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [streaming, setStreaming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);
  const bottomRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' });
  }, [messages]);

  useEffect(() => () => abortRef.current?.abort(), []);

  const handleSend = async () => {
    const message = input.trim();
    if (!message || streaming) return;

    setError(null);
    setInput('');
    const history = messages;
    setMessages([
      ...history,
      { role: 'user', content: message },
      { role: 'assistant', content: '' },
    ]);
    setStreaming(true);

    const controller = new AbortController();
    abortRef.current = controller;

    try {
      await streamChatReply(
        storyId,
        history,
        message,
        (chunk) => {
          setMessages((prev) => {
            const next = prev.slice();
            const last = next[next.length - 1];
            if (last && last.role === 'assistant') {
              next[next.length - 1] = { ...last, content: last.content + chunk };
            }
            return next;
          });
        },
        controller.signal,
      );
    } catch (err) {
      if ((err as { name?: string }).name === 'AbortError') return;
      setError(err instanceof Error ? err.message : 'Chat request failed.');
      setMessages((prev) => {
        const last = prev[prev.length - 1];
        if (last && last.role === 'assistant' && last.content === '') {
          return prev.slice(0, -1);
        }
        return prev;
      });
    } finally {
      setStreaming(false);
      abortRef.current = null;
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      void handleSend();
    }
  };

  return (
    <div className="flex flex-col h-[calc(100vh-12rem)]">
      <ScrollArea className="flex-1 pr-2">
        {messages.length === 0 ? (
          <p className="text-sm text-muted-foreground italic px-1 py-4">
            Ask about the plot, characters, or clues in this case.
          </p>
        ) : (
          <ul className="space-y-3 py-2">
            {messages.map((m, i) => (
              <li
                key={i}
                className={cn(
                  'rounded-md px-3 py-2 text-sm whitespace-pre-wrap',
                  m.role === 'user'
                    ? 'bg-secondary text-secondary-foreground ml-6'
                    : 'border border-border text-foreground mr-6',
                )}
              >
                {m.content || (
                  <span className="text-muted-foreground">…</span>
                )}
              </li>
            ))}
          </ul>
        )}
        <div ref={bottomRef} />
      </ScrollArea>

      {error && (
        <div
          role="alert"
          className="mt-2 flex items-start gap-2 rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          <AlertCircle className="size-4 mt-0.5 shrink-0" aria-hidden />
          <p className="leading-snug">{error}</p>
        </div>
      )}

      <form
        className="mt-2 flex gap-2 items-end"
        onSubmit={(e) => {
          e.preventDefault();
          void handleSend();
        }}
      >
        <textarea
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          rows={2}
          disabled={streaming}
          placeholder="Ask Watson…"
          className="flex-1 resize-none rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-primary/40 disabled:opacity-50"
        />
        <Button
          type="submit"
          size="icon"
          disabled={streaming || !input.trim()}
          aria-label="Send message"
        >
          <Send />
        </Button>
      </form>
    </div>
  );
}
