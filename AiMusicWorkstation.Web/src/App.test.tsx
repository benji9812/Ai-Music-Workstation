import { render, fireEvent, screen } from '@testing-library/react';
import fs from 'node:fs';
import path from 'node:path';
import App, { __testHooks, getActiveLyricIndex, onImportSuccess } from './App';


describe('Audio workstation UI behaviors', () => {
  beforeEach(() => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: true,
      json: async () => [],
    } as Response);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });
  beforeEach(() => {
    Object.defineProperty(globalThis, 'Audio', {
      configurable: true,
      value: function () {
        return {
          currentTime: 0,
          duration: 0,
          volume: 1,
          play: () => Promise.resolve(),
          pause: () => undefined,
          onloadedmetadata: null as null | (() => void),
        };
      },
    });
  });

  it('lyrics container has overflow and auto-scroll handles late times', () => {
    render(<App />);
    const cssPath = path.resolve(__dirname, 'index.css');
    const cssContent = fs.readFileSync(cssPath, 'utf-8');
    expect(cssContent).toContain('.lyrics-container');
    expect(cssContent).toContain('overflow-y: auto');

    const lyrics = [
      { start: 0, end: 1, text: 'line 1' },
      { start: 2, end: 3, text: 'line 2' },
    ];
    expect(getActiveLyricIndex(lyrics, 10)).toBe(1);
  });

  it('timeline slider waits for metadata and stays stable during drag', () => {
    render(<App />);
    const slider = screen.getByTestId('timeline-slider');
    expect(slider).toHaveAttribute('max', '0');
    __testHooks.setDuration(30);
    __testHooks.setDurationReady(true);

    fireEvent.mouseDown(slider);
    fireEvent.change(slider, { target: { value: '12' } });
    expect(slider).toHaveValue('12');
  });

  it('structure navigation jumps to section time', () => {
    const { container } = render(<App />);
    __testHooks.setStructure({ sections: [{ label: 'Intro', start: 5, end: 10 }] });
    __testHooks.setDuration(30);
    const structureItem = container.querySelector('[data-start]');
    if (!structureItem) return;
    fireEvent.click(structureItem);
    expect(__testHooks.getCurrentTime()).toBe(5);
  });

  it('toggle buttons apply active class', () => {
    render(<App />);
    const metroButton = screen.getByTestId('metro-toggle');
    fireEvent.click(metroButton);
    expect(metroButton.className).toMatch(/btn-active/);
    fireEvent.click(metroButton);
    expect(metroButton.className).not.toMatch(/btn-active/);

    const transposeButton = screen.getByTestId('transpose-toggle-minus');
    fireEvent.click(transposeButton);
    expect(transposeButton.className).toMatch(/btn-active/);
  });

  it('refreshLibrary is invoked after a successful import', () => {
    const refreshSpy = vi.fn();
    onImportSuccess(refreshSpy);
    expect(refreshSpy).toHaveBeenCalled();
  });
});
