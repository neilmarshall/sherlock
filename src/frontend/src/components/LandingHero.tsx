import { DrawCaseButton } from './DrawCaseButton';

interface LandingHeroProps {
  onDraw: () => void;
  loading: boolean;
}

export function LandingHero({ onDraw, loading }: LandingHeroProps) {
  return (
    <div className="flex flex-col items-center justify-center min-h-screen px-4 text-center">
      <div className="border border-primary/30 rounded-lg p-12 max-w-lg w-full bg-card/50">
        <h1 className="font-['Cinzel',serif] text-4xl md:text-5xl text-primary mb-4 tracking-wide">
          221B Baker Street
        </h1>
        <p className="font-['Playfair_Display',serif] text-lg md:text-xl text-muted-foreground mb-8 italic">
          Step into the world of Sherlock Holmes
        </p>
        <div className="w-16 mx-auto mb-8 border-t border-primary/40" />
        <DrawCaseButton onClick={onDraw} loading={loading} className="text-lg px-8 py-3" />
      </div>
    </div>
  );
}
