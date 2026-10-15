import { useEffect, useId, useRef, useState, type ReactNode } from 'react'

export interface MenuItem {
  label: string
  onSelect: () => void
  danger?: boolean
}

/**
 * A "⋯" button with a short list of actions, for rows and toolbars that would otherwise carry several
 * buttons each. Closes on choice, Escape or a click elsewhere.
 */
export function Menu({
  label,
  items,
  icon,
  triggerClassName,
  above = false,
}: {
  label: string
  items: MenuItem[]
  icon?: ReactNode
  /** Replaces the round icon button's look, e.g. a labelled tool button. */
  triggerClassName?: string
  /** Opens upwards, for a menu near the bottom of the screen. */
  above?: boolean
}) {
  const [open, setOpen] = useState(false)
  const id = useId()
  const root = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const close = (e: Event) => {
      if (
        e instanceof KeyboardEvent ? e.key === 'Escape' : !root.current?.contains(e.target as Node)
      ) {
        setOpen(false)
      }
    }
    document.addEventListener('keydown', close)
    document.addEventListener('mousedown', close)
    return () => {
      document.removeEventListener('keydown', close)
      document.removeEventListener('mousedown', close)
    }
  }, [open])

  return (
    <div ref={root} className="relative">
      <button
        type="button"
        aria-label={label}
        aria-haspopup="true"
        aria-expanded={open}
        aria-controls={id}
        onClick={() => {
          setOpen(!open)
        }}
        className={
          triggerClassName ??
          'flex min-h-12 min-w-12 items-center justify-center rounded-xl text-ink-muted hover:bg-accent-soft focus-visible:outline-4 focus-visible:outline-accent'
        }
      >
        {icon ?? (
          <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
            <circle cx="5" cy="12" r="2" />
            <circle cx="12" cy="12" r="2" />
            <circle cx="19" cy="12" r="2" />
          </svg>
        )}
      </button>
      {open && (
        <ul
          id={id}
          className={`absolute right-0 z-30 flex min-w-48 flex-col rounded-2xl bg-surface p-1.5 shadow-card ring-1 ring-line-soft ${above ? 'bottom-full mb-1' : 'mt-1'}`}
        >
          {items.map((item) => (
            <li key={item.label}>
              <button
                type="button"
                onClick={() => {
                  setOpen(false)
                  item.onSelect()
                }}
                className={`flex min-h-12 w-full items-center rounded-xl px-3 text-left font-semibold hover:bg-accent-soft focus-visible:outline-4 focus-visible:outline-accent ${item.danger ? 'text-danger' : 'text-ink'}`}
              >
                {item.label}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
