import { describe, expect, it } from "vitest";
import { applyLiveStemGain, calculateStemGain } from "./mixerGain";

describe("calculateStemGain", () => {
  it("normalizes the stem and master sliders", () => {
    expect(calculateStemGain(50, 80, false, false, false)).toBeCloseTo(0.4);
  });

  it("supports full-scale and missing stem volumes", () => {
    expect(calculateStemGain(100, 100, false, false, false)).toBe(1);
    expect(calculateStemGain(undefined, 100, false, false, false)).toBe(0);
  });

  it("clamps out-of-range values", () => {
    expect(calculateStemGain(150, 100, false, false, false)).toBe(1);
    expect(calculateStemGain(-10, 100, false, false, false)).toBe(0);
  });

  it("applies mute and solo state after gain normalization", () => {
    expect(calculateStemGain(80, 100, true, true, true)).toBe(0);
    expect(calculateStemGain(80, 100, false, false, true)).toBe(0);
    expect(calculateStemGain(80, 100, false, true, true)).toBeCloseTo(0.8);
  });
});

describe("applyLiveStemGain", () => {
  it("moves gain from the media element to the GainNode when routing begins", () => {
    const mediaElement = { volume: 0.8 } as HTMLAudioElement;
    const gainNode = { gain: { value: 1 } } as GainNode;

    applyLiveStemGain(mediaElement, gainNode, 0.4);

    expect(mediaElement.volume).toBe(1);
    expect(gainNode.gain.value).toBe(0.4);

    applyLiveStemGain(mediaElement, gainNode, 0.6);

    expect(mediaElement.volume).toBe(1);
    expect(gainNode.gain.value).toBe(0.6);
  });
});
