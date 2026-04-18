import type { Story } from '../types/story';
import type { ChatMessage } from '../types/chat';

export async function fetchRandomStory(): Promise<Story> {
  const res = await fetch('/api/stories/random');
  if (!res.ok) throw new Error(`Failed to fetch story: ${res.status}`);
  return res.json();
}

export async function streamChatReply(
  storyId: string,
  history: ChatMessage[],
  message: string,
  onToken: (chunk: string) => void,
  signal?: AbortSignal,
): Promise<void> {
  const res = await fetch(`/api/stories/${encodeURIComponent(storyId)}/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ history, message }),
    signal,
  });
  if (!res.ok || !res.body) throw new Error(`Chat failed: ${res.status}`);

  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  for (;;) {
    const { value, done } = await reader.read();
    if (done) break;
    const chunk = decoder.decode(value, { stream: true });
    if (chunk) onToken(chunk);
  }
  const tail = decoder.decode();
  if (tail) onToken(tail);
}
