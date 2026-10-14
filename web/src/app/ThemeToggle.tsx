import { useTheme, type ThemePreference } from './theme'

const choices: { value: ThemePreference; label: string; icon: string }[] = [
  { value: 'system', label: 'System', icon: '◐' },
  { value: 'light', label: 'Light', icon: '☀' },
  { value: 'dark', label: 'Dark', icon: '☾' },
]

/** Light, dark, or follow the device (FR-50); remembered on this device. */
export function ThemeToggle() {
  const [preference, setPreference] = useTheme()
  return (
    <div role="radiogroup" aria-label="Theme" className="flex rounded-xl border border-line p-0.5">
      {choices.map((choice) => (
        <button
          key={choice.value}
          type="button"
          role="radio"
          aria-checked={preference === choice.value}
          onClick={() => {
            setPreference(choice.value)
          }}
          className={`flex min-h-12 min-w-12 items-center justify-center gap-1 rounded-lg px-2 text-sm font-medium focus-visible:outline-4 focus-visible:outline-accent ${
            preference === choice.value
              ? 'bg-accent text-accent-ink'
              : 'text-ink-muted hover:bg-accent-soft'
          }`}
        >
          <span aria-hidden="true">{choice.icon}</span>
          <span className="sr-only sm:not-sr-only">{choice.label}</span>
        </button>
      ))}
    </div>
  )
}
