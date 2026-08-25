import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { App } from './App.tsx';

describe('App', () => {
  it('monta el andamiaje y expone un encabezado accesible', () => {
    render(<App />);

    // Consultas por rol accesible, no por clase ni por texto suelto
    // (`stacks/react/convenciones.md`).
    expect(screen.getByRole('heading', { level: 1, name: 'Bastion' })).toBeInTheDocument();
  });
});
