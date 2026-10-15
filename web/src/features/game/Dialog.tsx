import { useEffect, useRef, type ReactNode } from 'react'

/**
 * A modal over the page: a sheet from the bottom on phones, centred on wider screens. Focus moves into it
 * and back afterwards; Escape and the backdrop close it unless `dismissible` is false (Options asks first
 * about unapplied changes, so it closes itself). Never used on a player's card screen, which must not be
 * covered (docs/06).
 */
export function Dialog({
  label,
  onClose,
  dismissible = true,
  children,
}: {
  label: string
  onClose: () => void
  dismissible?: boolean
  children: ReactNode
}) {
  const box = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const before = document.activeElement as HTMLElement | null
    box.current?.focus()
    return () => {
      before?.focus()
    }
  }, [])

  useEffect(() => {
    if (!dismissible) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('keydown', onKey)
    }
  }, [dismissible, onClose])

  return (
    <div
      className="fixed inset-0 z-40 flex items-end justify-center bg-[rgb(20_15_38/0.55)] sm:items-center sm:p-6"
      onClick={(e) => {
        if (dismissible && e.target === e.currentTarget) onClose()
      }}
    >
      <div
        ref={box}
        role="dialog"
        aria-modal="true"
        aria-label={label}
        tabIndex={-1}
        className="max-h-[90dvh] w-full overflow-y-auto rounded-t-3xl bg-page p-2 outline-none sm:max-w-xl sm:rounded-3xl"
      >
        {children}
      </div>
    </div>
  )
}
