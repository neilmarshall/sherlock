import { Button } from '@/components/ui/button';

interface DrawCaseButtonProps {
  onClick: () => void;
  loading: boolean;
  className?: string;
}

export function DrawCaseButton({ onClick, loading, className = '' }: DrawCaseButtonProps) {
  return (
    <Button
      onClick={onClick}
      disabled={loading}
      className={`font-['Cinzel',serif] text-primary bg-secondary border border-primary/40 hover:bg-primary hover:text-primary-foreground transition-colors ${className}`}
    >
      {loading ? 'Drawing...' : 'Draw a Case'}
    </Button>
  );
}
