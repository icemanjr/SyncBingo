import type { ReactNode } from 'react'
import { useTheme, type ThemePreference } from './theme'

const icon = (path: ReactNode) => (
  <svg
    aria-hidden="true"
    width="18"
    height="18"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth="2"
    strokeLinecap="round"
    strokeLinejoin="round"
  >
    {path}
  </svg>
)

// Drawn icons, not text symbols: some phones' fonts lack ◐, which showed as a stray mark.
const choices: { value: ThemePreference; label: string; icon: ReactNode }[] = [
  {
    value: 'system',
    label: 'System',
    icon: icon(
      <>
        <rect x="3" y="4" width="18" height="12" rx="2" />
        <path d="M8 20h8M12 16v4" />
      </>,
    ),
  },
  {
    value: 'light',
    label: 'Light',
    icon: icon(
      <>
        <circle cx="12" cy="12" r="4" />
        <path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
      </>,
    ),
  },
  {
    value: 'dark',
    label: 'Dark',
    icon: icon(<path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z" />),
  },
]

/** Light, dark, or follow the device (FR-50); remembered on this device. */
export function ThemeToggle() {
  const [preference, setPreference] = useTheme()
  return (
    <div
      role="radiogroup"
      aria-label="Theme"
      className="flex gap-0.5 rounded-2xl bg-accent-soft p-1"
    >
      {choices.map((choice) => (
        <button
          key={choice.value}
          type="button"
          role="radio"
          aria-checked={preference === choice.value}
          aria-label={choice.label}
          title={choice.label}
          onClick={() => {
            setPreference(choice.value)
          }}
          className={`flex min-h-12 min-w-12 items-center justify-center rounded-xl focus-visible:outline-4 focus-visible:outline-accent ${
            preference === choice.value
              ? 'bg-surface text-ink shadow-sm'
              : 'text-accent-text hover:bg-surface/60'
          }`}
        >
          {choice.icon}
        </button>
      ))}
    </div>
  )
}
