import type { Story } from '../types/story';

export async function fetchRandomStory(): Promise<Story> {
  const res = await fetch('/api/stories/random');
  if (!res.ok) throw new Error(`Failed to fetch story: ${res.status}`);
  return res.json();
}
