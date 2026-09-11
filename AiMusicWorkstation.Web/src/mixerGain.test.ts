import { describe, expect, it } from "vitest";
import { calculateStemGain } from "./mixerGain";

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
