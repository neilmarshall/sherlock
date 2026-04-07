import { useState } from 'react';
import type { Story } from '@/types/story';
import { fetchRandomStory } from '@/lib/api';
import { LandingHero } from '@/components/LandingHero';
import { StoryView } from '@/components/StoryView';
import { DrawCaseButton } from '@/components/DrawCaseButton';
import { MetadataPanel } from '@/components/MetadataPanel';

function App() {
  const [story, setStory] = useState<Story | null>(null);
  const [loading, setLoading] = useState(false);
  const [panelOpen, setPanelOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleDraw = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchRandomStory();
      setStory(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch story');
    } finally {
      setLoading(false);
    }
  };

  if (!story) {
    return (
      <div className="min-h-screen bg-background">
        <LandingHero onDraw={handleDraw} loading={loading} />
        {error && (
          <div className="fixed bottom-4 left-1/2 -translate-x-1/2 bg-destructive text-destructive-foreground px-4 py-2 rounded-md text-sm">
            {error}
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background">
      <header className="sticky top-0 z-10 bg-card/95 backdrop-blur border-b border-border">
        <div className="max-w-5xl mx-auto px-4 py-3 flex items-center justify-between gap-2">
          <h1 className="font-['Cinzel',serif] text-primary text-lg tracking-wide hidden sm:block">
            221B Baker Street
          </h1>
          <div className="flex items-center gap-2 ml-auto">
            <DrawCaseButton onClick={handleDraw} loading={loading} />
            <MetadataPanel story={story} open={panelOpen} onOpenChange={setPanelOpen} />
          </div>
        </div>
      </header>
      <main>
        <StoryView key={story.id} story={story} />
      </main>
      {error && (
        <div className="fixed bottom-4 left-1/2 -translate-x-1/2 bg-destructive text-destructive-foreground px-4 py-2 rounded-md text-sm">
          {error}
        </div>
      )}
    </div>
  );
}

export default App;
