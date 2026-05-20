import '@testing-library/jest-dom';

class AudioContextMock {
  state = 'running';
  currentTime = 0;
  resume = () => Promise.resolve();
  createOscillator = () => ({
    connect: () => undefined,
    frequency: { value: 0 },
    start: () => undefined,
    stop: () => undefined,
  });
  createGain = () => ({
    connect: () => undefined,
    gain: {
      setValueAtTime: () => undefined,
      exponentialRampToValueAtTime: () => undefined,
    },
  });
}

Object.defineProperty(window, 'AudioContext', {
  configurable: true,
  value: AudioContextMock,
});

Object.defineProperty(window, 'webkitAudioContext', {
  configurable: true,
  value: AudioContextMock,
});
