export function calculateStemGain(
  stemVolume: number | undefined,
  masterVolume: number,
  isMuted: boolean,
  isSoloed: boolean,
  hasSolo: boolean,
): number {
  if (isMuted || (hasSolo && !isSoloed)) return 0;

  const gain = ((stemVolume ?? 0) / 100) * (masterVolume / 100);
  return Math.min(Math.max(gain, 0), 1);
}
