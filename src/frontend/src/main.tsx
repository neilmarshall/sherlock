import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import '@fontsource/lora';
import '@fontsource/playfair-display';
import '@fontsource/cinzel';
import './index.css';
import App from './App.tsx';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
