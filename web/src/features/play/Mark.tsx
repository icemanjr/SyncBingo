import type { MarkStyle } from './markStyle'

/**
 * A mark (FR-51), drawn *behind* a square's text: the square passes `layer="behind"`. When image squares
 * arrive (ADR 0009) they pass `layer="over"` and the mark becomes a translucent overlay instead.
 */
export function Mark({ style, layer = 'behind' }: { style: MarkStyle; layer?: 'behind' | 'over' }) {
  const common = { opacity: style.opacity, color: style.color }
  const z = layer === 'over' ? 'z-20' : 'z-0'
  switch (style.shape) {
    case 'tint':
      return (
        <span
          aria-hidden="true"
          className={`absolute inset-0 rounded-lg ${z}`}
          style={{ ...common, backgroundColor: style.color }}
        />
      )
    case 'square':
      return (
        <span
          aria-hidden="true"
          className={`absolute inset-1.5 rounded-md ${z}`}
          style={{ ...common, backgroundColor: style.color }}
        />
      )
    case 'circle':
      return (
        <span
          aria-hidden="true"
          className={`absolute inset-1 rounded-full ${z}`}
          style={{ ...common, backgroundColor: style.color }}
        />
      )
    case 'star':
    case 'heart':
      return (
        <svg
          aria-hidden="true"
          viewBox="0 0 24 24"
          className={`absolute inset-0.5 h-[calc(100%-4px)] w-[calc(100%-4px)] ${z}`}
          style={common}
        >
          <path
            fill="currentColor"
            d={
              style.shape === 'star'
                ? 'M12 1.5l3.1 6.6 7.2.9-5.3 5 1.4 7.1L12 17.6l-6.4 3.5L7 14l-5.3-5 7.2-.9z'
                : 'M12 21.3l-1.5-1.3C5.3 15.3 2 12.3 2 8.5 2 5.4 4.4 3 7.5 3c1.7 0 3.4.8 4.5 2.1C13.1 3.8 14.8 3 16.5 3 19.6 3 22 5.4 22 8.5c0 3.8-3.3 6.8-8.5 11.5z'
            }
          />
        </svg>
      )
  }
}
