import type { Story } from '@/types/story';

interface StoryViewProps {
  story: Story;
}

function renderBody(body: string) {
  const paragraphs = body.split(/\n\n+/);
  return paragraphs.map((para, i) => {
    const trimmed = para.trim();
    if (!trimmed) return null;
    // Convert _word_ patterns to <em>
    const parts = trimmed.split(/(_[^_]+_)/g);
    const elements = parts.map((part, j) => {
      const match = part.match(/^_(.+)_$/);
      if (match) {
        return <em key={j}>{match[1]}</em>;
      }
      return part;
    });
    return (
      <p key={i} className="mb-4">
        {elements}
      </p>
    );
  });
}

export function StoryView({ story }: StoryViewProps) {
  return (
    <article className="animate-fade-in max-w-3xl mx-auto px-4 sm:px-6 py-8">
      <h2 className="font-['Playfair_Display',serif] text-3xl md:text-4xl text-primary mb-2">
        {story.title}
      </h2>
      <p className="text-muted-foreground text-sm mb-6">
        {story.collection} &middot; {story.yearPublished}
      </p>
      <div className="text-primary/60 text-center mb-8 tracking-widest">
        &mdash; &#10022; &mdash;
      </div>
      <div className="leading-[1.8] text-foreground whitespace-pre-line">
        {renderBody(story.body)}
      </div>
    </article>
  );
}
